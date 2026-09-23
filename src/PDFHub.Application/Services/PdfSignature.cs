namespace PDFHub.Application.Services;

public static class PdfSignature
{
    /// <summary>Checks for the "%PDF-" signature, which the spec allows anywhere in the first 1024 bytes.</summary>
    public static async Task<bool> IsPdfAsync(Stream stream, CancellationToken ct = default)
    {
        var buffer = new byte[1024];
        var read = await stream.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
        return buffer.AsSpan(0, read).IndexOf("%PDF-"u8) >= 0;
    }
}
