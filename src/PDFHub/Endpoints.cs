using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using PDFHub.Data;
using PDFHub.Services;

namespace PDFHub;

public static class Endpoints
{
    public static void MapPdfHubEndpoints(this IEndpointRouteBuilder app)
    {
        // /pdf/NB-06618.pdf — the ".pdf" ending makes the browser's viewer and "Save as" use the right file name.
        app.MapGet("/pdf/{file}", async (string file, bool? download, AppDbContext db, PdfStorage storage, HttpContext http) =>
        {
            var code = PdfCodeRules.Normalize(Path.GetFileNameWithoutExtension(file));
            var drawing = await db.Drawings.AsNoTracking().FirstOrDefaultAsync(d => d.PdfCode == code);
            if (drawing?.PdfFileName is null)
                return Results.NotFound();

            var path = storage.FullPath(drawing.PdfFileName);
            if (!File.Exists(path))
                return Results.NotFound();

            var disposition = new ContentDispositionHeaderValue(download == true ? "attachment" : "inline");
            disposition.SetHttpFileName($"{code}.pdf");
            http.Response.Headers.ContentDisposition = disposition.ToString();
            return Results.File(path, "application/pdf", lastModified: File.GetLastWriteTimeUtc(path), enableRangeProcessing: true);
        });

        // Live check while typing a PdfCode on the drawing form.
        app.MapGet("/api/pdfcode", async (string? code, int? id, PdfCodeValidator validator) =>
        {
            var check = await validator.CheckAsync(code, id);
            return Results.Ok(new { code = check.Code, valid = check.IsValid, error = check.Error, section = check.Section?.Name });
        }).RequireAuthorization(Policies.CanEdit);
    }
}
