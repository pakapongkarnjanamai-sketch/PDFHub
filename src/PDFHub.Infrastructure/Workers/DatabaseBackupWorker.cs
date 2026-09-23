using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFHub.Application;
using PDFHub.Infrastructure.Persistence;

namespace PDFHub.Infrastructure.Workers;

/// <summary>
/// Makes one consistent copy of the database per day (backup/pdfhub-yyyyMMdd.db) with VACUUM INTO, which is
/// safe while the app is in use. PDFs are plain files; copy them off the server with scripts/Backup-Data.ps1.
/// Under IIS this needs the app pool to stay alive overnight (AlwaysRunning + preload), see Doc/DEPLOYMENT.md.
/// </summary>
public sealed class DatabaseBackupWorker(
    IServiceProvider serviceProvider, AppPaths paths, IOptions<PdfHubOptions> options, ILogger<DatabaseBackupWorker> logger)
    : BackgroundService
{
    private static readonly TimeSpan TickInterval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.BackupKeepDays <= 0)
            return;

        logger.LogInformation("{Worker} started.", nameof(DatabaseBackupWorker));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await BackupTodayAsync(stoppingToken);
                DeleteOldBackups();
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "{Worker} encountered an error; retrying on next tick.", nameof(DatabaseBackupWorker));
            }

            try
            {
                await Task.Delay(TickInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
        logger.LogInformation("{Worker} stopped.", nameof(DatabaseBackupWorker));
    }

    private async Task BackupTodayAsync(CancellationToken ct)
    {
        var target = Path.Combine(paths.BackupRoot, $"pdfhub-{DateTime.Now:yyyyMMdd}.db");
        if (File.Exists(target))
            return;

        // VACUUM INTO refuses an existing file; write to a temp name so a half-made copy never counts as today's.
        var temp = target + ".tmp";
        File.Delete(temp);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", [temp], ct);
        File.Move(temp, target);
        logger.LogInformation("Database backed up to {File}", target);
    }

    private void DeleteOldBackups()
    {
        var cutoff = DateTime.Now.AddDays(-options.Value.BackupKeepDays);
        foreach (var file in new DirectoryInfo(paths.BackupRoot).EnumerateFiles("pdfhub-*.db"))
            if (file.LastWriteTime < cutoff)
                file.Delete();
    }
}
