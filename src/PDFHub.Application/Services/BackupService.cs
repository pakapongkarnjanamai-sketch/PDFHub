using Microsoft.EntityFrameworkCore;
using PDFHub.Application.Abstractions;
using PDFHub.Application.Exceptions;
using PDFHub.Domain.DTOs;
using PDFHub.Domain.Entities;

namespace PDFHub.Application.Services;

public interface IBackupService
{
    Task<BackupOverviewDto> GetOverviewAsync(CancellationToken ct = default);
    BackupPathCheckDto CheckDestination(string? destination);
    /// <summary>Records the run and queues it; the copy happens in the background.</summary>
    Task<BackupRunDto> StartAsync(StartBackupDto dto, CancellationToken ct = default);
    /// <summary>Called by the background worker for a queued run.</summary>
    Task ExecuteAsync(int backupRunId, CancellationToken ct = default);
    /// <summary>Marks runs left "Running" by a restart as failed.</summary>
    Task FailInterruptedAsync(CancellationToken ct = default);
}

public sealed class BackupService(IUnitOfWork uow, IDataBackupStore store, IBackupQueue queue, IDateTime clock)
    : IBackupService
{
    private const int HistorySize = 30;

    private IRepository<BackupRun> Runs => uow.Repository<BackupRun>();

    public async Task<BackupOverviewDto> GetOverviewAsync(CancellationToken ct = default)
    {
        var history = await ToDtos(Runs.GetAll().AsNoTracking().OrderByDescending(r => r.StartedAt).Take(HistorySize)).ToListAsync(ct);
        return new BackupOverviewDto
        {
            LastDestination = await Runs.GetAll().Where(r => r.Status == BackupStatus.Succeeded)
                .OrderByDescending(r => r.StartedAt).Select(r => r.Destination).FirstOrDefaultAsync(ct) ?? string.Empty,
            Running = history.FirstOrDefault(r => r.Status == nameof(BackupStatus.Running)),
            History = history,
            DailyBackupFolder = store.DailyBackupFolder,
        };
    }

    public BackupPathCheckDto CheckDestination(string? destination)
    {
        var path = (destination ?? string.Empty).Trim();
        var error = store.CheckDestination(path);
        return new BackupPathCheckDto(path, error is null, error ?? "เขียนไฟล์ลงปลายทางนี้ได้");
    }

    public async Task<BackupRunDto> StartAsync(StartBackupDto dto, CancellationToken ct = default)
    {
        var destination = (dto.Destination ?? string.Empty).Trim();
        if (store.CheckDestination(destination) is { } error)
            throw ValidationException.For(nameof(dto.Destination), error);
        if (await Runs.GetAll().AnyAsync(r => r.Status == BackupStatus.Running, ct))
            throw new BusinessRuleException("กำลังสำรองข้อมูลอยู่ รอให้เสร็จก่อนแล้วค่อยเริ่มครั้งใหม่");

        var run = Runs.New();
        run.Destination = destination;
        run.Status = BackupStatus.Running;
        run.StartedAt = clock.Now;
        await Runs.AddAsync(run);
        await uow.CommitAsync();

        queue.Enqueue(run.Id);
        return (await ToDtos(Runs.GetAll().AsNoTracking().Where(r => r.Id == run.Id)).SingleAsync(ct));
    }

    public async Task ExecuteAsync(int backupRunId, CancellationToken ct = default)
    {
        var run = await Runs.GetByIdAsync(backupRunId);
        if (run is null || run.Status != BackupStatus.Running)
            return;

        try
        {
            run.DatabaseFile = await store.SnapshotDatabaseAsync(run.Destination, run.StartedAt, ct);
            await uow.CommitAsync();

            // Progress is saved at most every 2 seconds so the page can show it without a write per file.
            var lastSave = DateTime.UtcNow;
            var pending = (PdfCopyProgress?)null;
            var progress = new Progress<PdfCopyProgress>(p => pending = p);
            var copyTask = store.CopyPdfsAsync(run.Destination, progress, ct);
            while (!copyTask.IsCompleted)
            {
                await Task.WhenAny(copyTask, Task.Delay(TimeSpan.FromSeconds(2), ct));
                if (pending is { } p && DateTime.UtcNow - lastSave > TimeSpan.FromSeconds(2))
                {
                    Apply(run, p);
                    await uow.CommitAsync();
                    lastSave = DateTime.UtcNow;
                }
            }

            Apply(run, await copyTask);
            run.Status = BackupStatus.Succeeded;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            run.Status = BackupStatus.Failed;
            run.Error = ex switch
            {
                UnauthorizedAccessException => $"ไม่มีสิทธิ์เขียนไฟล์ที่ปลายทาง: {ex.Message}",
                IOException => $"เขียนไฟล์ไม่สำเร็จ (ดิสก์เต็มหรือเครือข่ายขาด?): {ex.Message}",
                _ => ex.Message,
            };
        }
        finally
        {
            if (run.Status == BackupStatus.Running)
            {
                run.Status = BackupStatus.Failed;
                run.Error = "หยุดกลางคันเพราะระบบปิดตัว";
            }
            run.FinishedAt = clock.Now;
            await uow.CommitAsync();
        }
    }

    public async Task FailInterruptedAsync(CancellationToken ct = default)
    {
        foreach (var run in await Runs.GetAll().Where(r => r.Status == BackupStatus.Running).ToListAsync(ct))
        {
            run.Status = BackupStatus.Failed;
            run.Error = "หยุดกลางคันเพราะระบบเริ่มทำงานใหม่ กรุณาสำรองข้อมูลอีกครั้ง";
            run.FinishedAt = clock.Now;
        }
        await uow.CommitAsync();
    }

    private static void Apply(BackupRun run, PdfCopyProgress p)
    {
        run.PdfTotal = p.Total;
        run.PdfCopied = p.Copied;
        run.PdfSkipped = p.Skipped;
        run.BytesCopied = p.BytesCopied;
    }

    private static IQueryable<BackupRunDto> ToDtos(IQueryable<BackupRun> query) => query.Select(r => new BackupRunDto
    {
        Id = r.Id,
        Destination = r.Destination,
        Status = r.Status.ToString(),
        StartedAt = r.StartedAt,
        FinishedAt = r.FinishedAt,
        DatabaseFile = r.DatabaseFile ?? "",
        PdfTotal = r.PdfTotal,
        PdfCopied = r.PdfCopied,
        PdfSkipped = r.PdfSkipped,
        BytesCopied = r.BytesCopied,
        Error = r.Error ?? "",
        StartedBy = r.CreatedBy ?? "",
    });
}
