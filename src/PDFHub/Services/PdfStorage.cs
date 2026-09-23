using System.Text;

namespace PDFHub.Services;

/// <summary>
/// Stores PDFs as {PdfRoot}/{section}/{PdfCode}.pdf. Files are never deleted: a replaced or removed
/// PDF is moved to {PdfRoot}/_archive so an old drawing revision can always be recovered.
/// </summary>
public sealed class PdfStorage(AppPaths paths)
{
    private const string ArchiveFolder = "_archive";

    public static string RelativePathFor(string pdfCode) => Path.Combine(pdfCode[..2], pdfCode + ".pdf");

    public string FullPath(string relativePath)
    {
        var full = Path.GetFullPath(Path.Combine(paths.PdfRoot, relativePath));
        if (!full.StartsWith(paths.PdfRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Path escapes the PDF folder: {relativePath}");
        return full;
    }

    /// <summary>Checks for the "%PDF-" signature, which the spec allows anywhere in the first 1024 bytes.</summary>
    public static async Task<bool> IsPdfAsync(Stream stream, CancellationToken ct = default)
    {
        var buffer = new byte[1024];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
        return buffer.AsSpan(0, read).IndexOf("%PDF-"u8) >= 0;
    }

    /// <summary>Saves (or replaces) the PDF for a code and returns its relative path.</summary>
    public async Task<string> SaveAsync(string pdfCode, Stream content, CancellationToken ct = default)
    {
        var relative = RelativePathFor(pdfCode);
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

    /// <summary>Renames the stored PDF when a drawing's code changes; returns the new relative path.</summary>
    public string Rename(string relativePath, string newCode)
    {
        var relative = RelativePathFor(newCode);
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
        var name = new StringBuilder(pdfCode).Append('_').Append(DateTime.Now.ToString("yyyyMMdd-HHmmss"))
            .Append('_').Append(reason).Append(".pdf").ToString();
        File.Move(fullPath, Path.Combine(folder, name), overwrite: true);
    }
}
