using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PDFHub.Data;
using PDFHub.Services;

namespace PDFHub.Pages.Import;

public class IndexModel(AppDbContext db, DrawingImporter importer, PdfStorage storage, IOptions<PdfHubOptions> options) : PageModel
{
    [BindProperty]
    public IFormFile? ExcelFile { get; set; }

    [BindProperty]
    public ImportMode Mode { get; set; } = ImportMode.SkipExisting;

    [BindProperty]
    public bool DryRun { get; set; } = true;

    public ImportResult? Result { get; private set; }
    public string? ExcelError { get; private set; }
    public int DrawingCount { get; private set; }
    public int MissingPdfCount { get; private set; }
    public int MaxPdfSizeMB => options.Value.MaxPdfSizeMB;

    public async Task OnGetAsync() => await LoadCountsAsync();

    public async Task<IActionResult> OnPostExcelAsync()
    {
        if (ExcelFile is not { Length: > 0 } || !ExcelFile.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            ExcelError = "กรุณาเลือกไฟล์ Excel นามสกุล .xlsx (ถ้าเป็น .xls หรือ .xlsm ให้ Save As เป็น .xlsx ก่อน)";
        }
        else
        {
            try
            {
                await using var stream = ExcelFile.OpenReadStream();
                Result = await importer.ImportAsync(stream, Mode, DryRun, User.Identity?.Name, HttpContext.RequestAborted);
            }
            // ClosedXML and the OpenXML SDK throw many unrelated exception types for damaged or non-Excel files.
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ExcelError = "อ่านไฟล์ Excel ไม่ได้: " + ex.Message;
            }
        }

        await LoadCountsAsync();
        return Page();
    }

    /// <summary>
    /// Called by the bulk uploader once per file. The file name must be the PdfCode (NB-06618.pdf),
    /// the same naming the old Excel hyperlinks used.
    /// </summary>
    public async Task<IActionResult> OnPostPdfAsync(IFormFile? file, bool overwrite)
    {
        if (file is null || file.Length == 0)
            return new JsonResult(new { status = "error", message = "ไม่มีไฟล์" });

        var code = PdfCodeRules.Normalize(Path.GetFileNameWithoutExtension(file.FileName));
        JsonResult Reply(string status, string message) => new(new { status, code, message });

        if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return Reply("skip", "ไม่ใช่ไฟล์ .pdf");
        if (PdfCodeRules.FormatError(code) is not null)
            return Reply("error", "ชื่อไฟล์ไม่ใช่ PdfCode (ต้องเป็นแบบ NB-06618.pdf)");
        if (file.Length > MaxPdfSizeMB * 1024L * 1024)
            return Reply("error", $"ไฟล์ใหญ่เกิน {MaxPdfSizeMB} MB");

        var drawing = await db.Drawings.FirstOrDefaultAsync(d => d.PdfCode == code);
        if (drawing is null)
            return Reply("error", "ไม่พบ PdfCode นี้ในฐานข้อมูล (ต้องเพิ่มข้อมูลหรือนำเข้า Excel ก่อน)");
        if (drawing.PdfFileName is not null && !overwrite)
            return Reply("skip", "มีไฟล์อยู่แล้ว (ข้าม)");

        await using (var check = file.OpenReadStream())
            if (!await PdfStorage.IsPdfAsync(check))
                return Reply("error", "เนื้อไฟล์ไม่ใช่ PDF");

        var replaced = drawing.PdfFileName is not null;
        await using (var stream = file.OpenReadStream())
            drawing.PdfFileName = await storage.SaveAsync(code, stream, HttpContext.RequestAborted);
        drawing.PdfSize = file.Length;
        drawing.PdfUploadedAt = drawing.UpdatedAt = DateTime.Now;
        drawing.UpdatedBy = User.Identity?.Name;
        await db.SaveChangesAsync();

        return Reply("ok", replaced ? "แทนที่ไฟล์เดิมแล้ว" : "อัปโหลดแล้ว");
    }

    private async Task LoadCountsAsync()
    {
        DrawingCount = await db.Drawings.CountAsync();
        MissingPdfCount = await db.Drawings.CountAsync(d => d.PdfFileName == null);
    }
}
