using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PDFHub.Data;

namespace PDFHub.Pages.Admin;

public class SectionsModel(AppDbContext db) : PageModel
{
    public List<(Section Section, int Drawings)> Rows { get; private set; } = [];

    [BindProperty]
    public string? NewCode { get; set; }

    [BindProperty]
    public string? NewName { get; set; }

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        var code = (NewCode ?? "").Trim().ToUpperInvariant();
        var name = (NewName ?? "").Trim();

        if (code.Length != 2 || !code.All(char.IsAsciiLetterUpper))
            ModelState.AddModelError(nameof(NewCode), "รหัสต้องเป็นตัวอักษรภาษาอังกฤษ 2 ตัว เช่น NB");
        else if (await db.Sections.AnyAsync(s => s.Code == code))
            ModelState.AddModelError(nameof(NewCode), $"มีรหัส {code} อยู่แล้ว");
        if (name.Length == 0)
            ModelState.AddModelError(nameof(NewName), "กรุณากรอกชื่อ Section");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        db.Sections.Add(new Section { Code = code, Name = name });
        await db.SaveChangesAsync();
        this.Flash($"เพิ่ม Section {code} · {name} แล้ว");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, string? name, bool active)
    {
        var section = await db.Sections.FindAsync(id);
        if (section is null)
            return NotFound();

        if (!string.IsNullOrWhiteSpace(name))
            section.Name = name.Trim();
        section.IsActive = active;
        await db.SaveChangesAsync();
        this.Flash($"บันทึก Section {section.Code} แล้ว");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var section = await db.Sections.FindAsync(id);
        if (section is null)
            return NotFound();

        if (await db.Drawings.AnyAsync(d => d.SectionId == id))
        {
            this.Flash($"ลบ {section.Code} ไม่ได้เพราะมี Drawing ใช้อยู่ ให้ปิดใช้งานแทน", "error");
            return RedirectToPage();
        }

        db.Sections.Remove(section);
        await db.SaveChangesAsync();
        this.Flash($"ลบ Section {section.Code} แล้ว", "warning");
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var counts = await db.Drawings.GroupBy(d => d.SectionId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var sections = await db.Sections.AsNoTracking().OrderBy(s => s.Code).ToListAsync();
        Rows = sections.Select(s => (s, counts.GetValueOrDefault(s.Id))).ToList();
    }
}
