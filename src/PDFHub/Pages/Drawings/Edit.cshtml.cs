using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PDFHub.Data;
using PDFHub.Services;

namespace PDFHub.Pages.Drawings;

[Authorize(Policy = Policies.CanEdit)]
public class EditModel(AppDbContext db, PdfCodeValidator validator, PdfStorage storage, IOptions<PdfHubOptions> options) : PageModel
{
    [BindProperty]
    public DrawingInput Input { get; set; } = new();

    [BindProperty]
    public IFormFile? PdfFile { get; set; }

    public Drawing? Existing { get; private set; }
    public bool IsNew => Existing is null;
    public List<Section> Sections { get; private set; } = [];
    public List<string> Materials { get; private set; } = [];
    public int MaxPdfSizeMB => options.Value.MaxPdfSizeMB;

    /// <param name="next">Pre-filled code after "save and add next".</param>
    /// <param name="date">Pre-filled input date after "save and add next".</param>
    public async Task<IActionResult> OnGetAsync(int? id, string? next, string? date)
    {
        if (id is not null)
        {
            Existing = await db.Drawings.Include(d => d.Section).FirstOrDefaultAsync(d => d.Id == id);
            if (Existing is null)
                return NotFound();
            Input = DrawingInput.From(Existing);
        }
        else
        {
            Input.PdfCode = next;
            Input.InputDate = DateOnly.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                ? d
                : DateOnly.FromDateTime(DateTime.Now);
        }

        await LoadListsAsync();
        return Page();
    }

    /// <param name="then">"next" = stay on the form for the following sticker code.</param>
    public async Task<IActionResult> OnPostAsync(int? id, string? then)
    {
        if (id is not null)
        {
            Existing = await db.Drawings.Include(d => d.Section).FirstOrDefaultAsync(d => d.Id == id);
            if (Existing is null)
                return NotFound();
        }

        var check = await validator.CheckAsync(Input.PdfCode, id);
        Input.PdfCode = check.Code;
        ModelState.Remove($"{nameof(Input)}.{nameof(Input.PdfCode)}");
        if (!check.IsValid)
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.PdfCode)}", check.Error!);

        var hasUpload = PdfFile is { Length: > 0 };
        if (hasUpload)
            await ValidatePdfAsync(PdfFile!);

        if (!ModelState.IsValid)
        {
            await LoadListsAsync();
            return Page();
        }

        var now = DateTime.Now;
        var user = User.Identity?.Name;
        var drawing = Existing ?? new Drawing { CreatedAt = now, CreatedBy = user };
        var oldCode = drawing.PdfCode;
        Input.ApplyTo(drawing);
        drawing.SectionId = check.Section!.Id;
        drawing.UpdatedAt = now;
        drawing.UpdatedBy = user;

        if (Existing is null)
            db.Drawings.Add(drawing);
        else if (oldCode != drawing.PdfCode && drawing.PdfFileName is not null)
            drawing.PdfFileName = storage.Rename(drawing.PdfFileName, drawing.PdfCode);

        if (hasUpload)
        {
            await using var stream = PdfFile!.OpenReadStream();
            drawing.PdfFileName = await storage.SaveAsync(drawing.PdfCode, stream, HttpContext.RequestAborted);
            drawing.PdfSize = PdfFile.Length;
            drawing.PdfUploadedAt = now;
        }

        try
        {
            await db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Someone else saved the same code between the check and this save.
            ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.PdfCode)}", $"PdfCode {drawing.PdfCode} มีอยู่ในระบบแล้ว");
            await LoadListsAsync();
            return Page();
        }

        this.Flash(IsNew ? $"เพิ่ม {drawing.PdfCode} เรียบร้อยแล้ว" : $"บันทึก {drawing.PdfCode} เรียบร้อยแล้ว");

        if (IsNew && then == "next")
            return RedirectToPage(new { next = PdfCodeRules.Next(drawing.PdfCode), date = drawing.InputDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });
        return RedirectToPage("/Drawings/Details", new { code = drawing.PdfCode });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        if (!User.IsInRole(Roles.Admin))
            return Forbid();

        var drawing = await db.Drawings.FirstOrDefaultAsync(d => d.Id == id);
        if (drawing is null)
            return NotFound();

        if (drawing.PdfFileName is not null)
            storage.Archive(drawing.PdfFileName, drawing.PdfCode, "deleted");
        db.Drawings.Remove(drawing);
        await db.SaveChangesAsync();

        this.Flash($"ลบ {drawing.PdfCode} แล้ว (ไฟล์ PDF ถูกย้ายไปเก็บในโฟลเดอร์ _archive)", "warning");
        return RedirectToPage("/Index");
    }

    private async Task ValidatePdfAsync(IFormFile file)
    {
        const string key = nameof(PdfFile);
        if (file.Length > MaxPdfSizeMB * 1024L * 1024)
        {
            ModelState.AddModelError(key, $"ไฟล์ใหญ่เกิน {MaxPdfSizeMB} MB");
            return;
        }
        await using var stream = file.OpenReadStream();
        if (!await PdfStorage.IsPdfAsync(stream))
            ModelState.AddModelError(key, "ไฟล์ที่เลือกไม่ใช่ไฟล์ PDF");
    }

    private async Task LoadListsAsync()
    {
        Sections = await db.Sections.AsNoTracking().OrderBy(s => s.Code).ToListAsync();
        Materials = await db.Drawings.Where(d => d.Material != null).Select(d => d.Material!).Distinct().OrderBy(m => m).ToListAsync();
    }
}

public class DrawingInput
{
    // Checked by PdfCodeValidator, which carries over the rules of the original Excel macro.
    public string? PdfCode { get; set; }

    [Required(ErrorMessage = "กรุณากรอก Part Name")]
    [StringLength(200)]
    public string? PartName { get; set; }

    [StringLength(100)]
    public string? DrawingNo { get; set; }

    [StringLength(100)]
    public string? Material { get; set; }

    [Range(0, 999_999_999, ErrorMessage = "ราคาต้องเป็นตัวเลขตั้งแต่ 0 ขึ้นไป")]
    public decimal? Price { get; set; }

    [Required(ErrorMessage = "กรุณาระบุวันที่")]
    public DateOnly? InputDate { get; set; }

    [StringLength(50)]
    public string? QuoNo { get; set; }

    [StringLength(1000)]
    public string? Remark { get; set; }

    public bool HasPo { get; set; }

    public static DrawingInput From(Drawing d) => new()
    {
        PdfCode = d.PdfCode,
        PartName = d.PartName,
        DrawingNo = d.DrawingNo,
        Material = d.Material,
        Price = d.Price,
        InputDate = d.InputDate,
        QuoNo = d.QuoNo,
        Remark = d.Remark,
        HasPo = d.HasPo,
    };

    public void ApplyTo(Drawing d)
    {
        d.PdfCode = PdfCode!;
        d.PartName = PartName!.Trim();
        d.DrawingNo = Clean(DrawingNo);
        d.Material = Clean(Material);
        d.Price = Price;
        d.InputDate = InputDate!.Value;
        d.QuoNo = Clean(QuoNo);
        d.Remark = Clean(Remark);
        d.HasPo = HasPo;
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
}
