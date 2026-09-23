using Microsoft.EntityFrameworkCore;
using PDFHub.Application.Abstractions;
using PDFHub.Infrastructure.Persistence;

namespace PDFHub.Infrastructure.Services;

/// <summary>
/// Writes a manual backup under {destination}\PDFHub\:
///   db\pdfhub-yyyyMMdd-HHmmss.db   a new consistent database copy per run
///   pdf\...                        the PDF folder (including _archive), copied incrementally
/// Under IIS the files are written as the app pool identity, which needs write access to the destination.
/// </summary>
public sealed class FileSystemDataBackupStore(AppPaths paths, AppDbContext db) : IDataBackupStore
{
    private const string BackupFolderName = "PDFHub";

    public string DailyBackupFolder => paths.BackupRoot;

    public string? CheckDestination(string destination)
    {
        if (string.IsNullOrWhiteSpace(destination))
            return "กรุณาระบุโฟลเดอร์ปลายทาง";
        if (!Path.IsPathFullyQualified(destination))
            return @"ต้องเป็น path เต็ม เช่น E:\PDFHubBackup หรือ \\NAS01\Backup";

        string target;
        try
        {
            target = Path.GetFullPath(Path.Combine(destination, BackupFolderName));
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return $"path ไม่ถูกต้อง: {ex.Message}";
        }

        // Backing up into the data folder (or the data folder into the backup) would copy the backup into itself.
        if (IsInside(target, paths.DataRoot) || IsInside(paths.DataRoot, target))
            return $"ห้ามเลือกโฟลเดอร์ที่ซ้อนกับโฟลเดอร์ข้อมูลของระบบ ({paths.DataRoot})";

        try
        {
            Directory.CreateDirectory(target);
            var probe = Path.Combine(target, $".write-test-{Guid.NewGuid():N}");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return $"ไม่มีสิทธิ์เขียนไฟล์ที่ {target} — ให้สิทธิ์ Modify กับบัญชีที่รันเว็บ ({Environment.UserDomainName}\\{Environment.UserName})";
        }
        catch (Exception ex) when (ex is IOException or System.Security.SecurityException)
        {
            return $"เข้าถึงปลายทางไม่ได้: {ex.Message}";
        }
    }

    public async Task<string> SnapshotDatabaseAsync(string destination, DateTime stamp, CancellationToken ct)
    {
        var relative = Path.Combine(BackupFolderName, "db", $"pdfhub-{stamp:yyyyMMdd-HHmmss}.db");
        var target = Path.Combine(destination, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);

        // VACUUM INTO gives a consistent copy while the app is in use. Write it locally first, then copy,
        // so a slow or flaky network share never holds a lock on the live database.
        var temp = Path.Combine(paths.BackupRoot, $"manual-{Guid.NewGuid():N}.db");
        try
        {
            await db.Database.ExecuteSqlRawAsync("VACUUM INTO {0}", [temp], ct);
            await CopyFileAsync(temp, target, ct);
        }
        finally
        {
            File.Delete(temp);
        }
        return relative;
    }

    public async Task<PdfCopyProgress> CopyPdfsAsync(string destination, IProgress<PdfCopyProgress> progress, CancellationToken ct)
    {
        var targetRoot = Path.Combine(destination, BackupFolderName, "pdf");
        var files = Directory.EnumerateFiles(paths.PdfRoot, "*", SearchOption.AllDirectories)
            .Where(f => !f.EndsWith(".uploading", StringComparison.OrdinalIgnoreCase))
            .ToList();

        int copied = 0, skipped = 0;
        long bytes = 0;
        progress.Report(new(files.Count, 0, 0, 0));

        foreach (var source in files)
        {
            ct.ThrowIfCancellationRequested();
            var target = Path.Combine(targetRoot, Path.GetRelativePath(paths.PdfRoot, source));
            var sourceInfo = new FileInfo(source);
            var targetInfo = new FileInfo(target);

            // Same size and timestamp = already backed up by an earlier run.
            if (targetInfo.Exists && targetInfo.Length == sourceInfo.Length && targetInfo.LastWriteTimeUtc == sourceInfo.LastWriteTimeUtc)
            {
                skipped++;
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                await CopyFileAsync(source, target, ct);
                File.SetLastWriteTimeUtc(target, sourceInfo.LastWriteTimeUtc);
                copied++;
                bytes += sourceInfo.Length;
            }
            progress.Report(new(files.Count, copied, skipped, bytes));
        }
        return new(files.Count, copied, skipped, bytes);
    }

    private static async Task CopyFileAsync(string source, string target, CancellationToken ct)
    {
        // Copy to a temp name and swap, so an interrupted copy never leaves a truncated file that looks complete.
        var temp = target + ".partial";
        await using (var input = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, useAsync: true))
        await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            await input.CopyToAsync(output, ct);
        File.Move(temp, target, overwrite: true);
    }

    private static bool IsInside(string path, string folder)
    {
        var p = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar;
        var f = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder)) + Path.DirectorySeparatorChar;
        return p.StartsWith(f, StringComparison.OrdinalIgnoreCase);
    }
}
