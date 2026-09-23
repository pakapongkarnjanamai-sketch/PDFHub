using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PDFHub.Data;

namespace PDFHub.Services;

/// <summary>
/// Makes one consistent copy of the database per day (backup/pdfhub-yyyyMMdd.db) using VACUUM INTO,
/// which is safe while the app is in use. PDFs are plain files; copy the pdf folder with deploy/backup.ps1.
/// </summary>
public sealed class DatabaseBackupService(
    IServiceScopeFactory scopes, AppPaths paths, IOptions<PdfHubOptions> options, ILogger<DatabaseBackupService> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.BackupKeepDays <= 0)
            return;

        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try
            {
                await BackupTodayAsync(stoppingToken);
                DeleteOldBackups();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Database backup failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task BackupTodayAsync(CancellationToken ct)
    {
        var target = Path.Combine(paths.BackupRoot, $"pdfhub-{DateTime.Now:yyyyMMdd}.db");
        if (File.Exists(target))
            return;

        var temp = target + ".tmp";
        File.Delete(temp);

        using var scope = scopes.CreateScope();
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
