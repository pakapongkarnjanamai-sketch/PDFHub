using Microsoft.Extensions.Options;
using PDFHub.Application;
using PDFHub.Application.Abstractions;
using PDFHub.Domain;

namespace PDFHub.Infrastructure.Services;

/// <summary>
/// Stores PDFs as {PdfRoot}/{section}/{PdfCode}.pdf. Files are never deleted: a replaced or removed
/// PDF is moved to {PdfRoot}/_archive so an old drawing revision can always be recovered.
/// Under IIS the app pool identity needs Modify rights on the data folder.
/// </summary>
public sealed class FileSystemPdfStorage(AppPaths paths, IOptions<PdfHubOptions> options) : IPdfStorage
{
    private const string ArchiveFolder = "_archive";

    public long MaxFileBytes => options.Value.MaxPdfSizeMB * 1024L * 1024;

    public string FullPath(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(paths.PdfRoot, relativePath));
        if (!full.StartsWith(paths.PdfRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Path escapes the PDF folder: {relativePath}");
        return full;
    }

    public async Task<string> SaveAsync(string pdfCode, Stream content, CancellationToken ct = default)
    {
        var relative = PdfCodeRules.RelativePdfPath(pdfCode);
        var full = FullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        // Write next to the target first so a failed upload never leaves a half-written PDF in place.
        var temp = full + ".uploading";
        await using (var file = File.Create(temp))
            await content.CopyToAsync(file, ct);

        if (File.Exists(full))
            MoveToArchive(full, pdfCode, "replaced");
        File.Move(temp, full);
        return relative;
    }

    public void Archive(string relativePath, string pdfCode, string reason)
    {
        var full = FullPath(relativePath);
        if (File.Exists(full))
            MoveToArchive(full, pdfCode, reason);
    }

    public string Rename(string relativePath, string newCode)
    {
        var relative = PdfCodeRules.RelativePdfPath(newCode);
        var source = FullPath(relativePath);
        if (!File.Exists(source))
            return relative;

        var target = FullPath(relative);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        if (File.Exists(target))
            MoveToArchive(target, newCode, "replaced");
        File.Move(source, target);
        return relative;
    }

    private void MoveToArchive(string fullPath, string pdfCode, string reason)
    {
        var folder = Path.Combine(paths.PdfRoot, ArchiveFolder, pdfCode[..2]);
        Directory.CreateDirectory(folder);
        File.Move(fullPath, Path.Combine(folder, $"{pdfCode}_{DateTime.Now:yyyyMMdd-HHmmss}_{reason}.pdf"), overwrite: true);
    }
}
