using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PDFHub.Api.Extensions;
using PDFHub.Application.Services;
using PDFHub.Domain.DTOs;

namespace PDFHub.Api.Controllers;

[ApiController]
[Route("api/drawings")]
public class DrawingsController(IDrawingService drawings) : ControllerBase
{
    [HttpGet]
    public Task<DrawingListResultDto> List([FromQuery] DrawingListQuery query, CancellationToken ct) =>
        drawings.ListAsync(query, ct);

    /// <summary>Every drawing matching the same query as the list (all pages), in the same order.</summary>
    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] DrawingListQuery query, CancellationToken ct)
    {
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var bytes = await drawings.ExportAsync(query, $"DATA DRAWING — PDFHub export {DateTime.Now:dd/MM/yyyy HH:mm}",
            code => $"{baseUrl}/pdf/{code}.pdf", ct);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"PDFHub-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }

    [HttpGet("lookups")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<DrawingLookupsDto> Lookups(CancellationToken ct) => drawings.GetLookupsAsync(ct);

    [HttpGet("check-code")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<PdfCodeCheckDto> CheckCode([FromQuery] string? code, [FromQuery] int? id, CancellationToken ct) =>
        drawings.CheckCodeAsync(code, id, ct);

    [HttpGet("{code}")]
    public Task<DrawingDetailDto> Get(string code, CancellationToken ct) => drawings.GetByCodeAsync(code, ct);

    [HttpPost]
    [Authorize(Policy = Policies.CanEdit)]
    public async Task<IActionResult> Create(CreateDrawingDto dto, CancellationToken ct)
    {
        var created = await drawings.CreateAsync(dto, ct);
        return CreatedAtAction(nameof(Get), new { code = created.PdfCode }, created);
    }

    [HttpPut("{id:int}")]
    [Authorize(Policy = Policies.CanEdit)]
    public Task<DrawingDetailDto> Update(int id, UpdateDrawingDto dto, CancellationToken ct) => drawings.UpdateAsync(id, dto, ct);

    [HttpDelete("{id:int}")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        await drawings.DeleteAsync(id, ct);
        return Ok(new { success = true });
    }

    [HttpPut("{id:int}/pdf")]
    [Authorize(Policy = Policies.CanEdit)]
    public async Task<DrawingDetailDto> UploadPdf(int id, IFormFile file, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await drawings.UploadPdfAsync(id, stream, file.Length, ct);
    }

    /// <summary>One file per request; the file name picks the drawing (NB-06618.pdf).</summary>
    [HttpPost("pdf-bulk")]
    [Authorize(Policy = Policies.CanEdit)]
    public async Task<PdfUploadResultDto> UploadPdfBulk(IFormFile file, [FromForm] bool overwrite, CancellationToken ct)
    {
        await using var stream = file.OpenReadStream();
        return await drawings.UploadPdfByFileNameAsync(file.FileName, stream, file.Length, overwrite, ct);
    }
}

/// <summary>/pdf/NB-06618.pdf — the ".pdf" ending makes the browser's viewer and "Save as" use the right file name.</summary>
[ApiController]
public class PdfController(IDrawingService drawings) : ControllerBase
{
    [HttpGet("/pdf/{file}")]
    public async Task<IActionResult> Get(string file, [FromQuery] bool download, CancellationToken ct)
    {
        var pdf = await drawings.GetPdfAsync(file, ct);
        if (pdf is null)
            return NotFound("ไม่พบไฟล์ PDF");

        if (download)
            return PhysicalFile(pdf.FullPath, "application/pdf", pdf.FileName, enableRangeProcessing: true);

        Response.Headers.ContentDisposition = $"inline; filename=\"{pdf.FileName}\"";
        return PhysicalFile(pdf.FullPath, "application/pdf", enableRangeProcessing: true);
    }
}
