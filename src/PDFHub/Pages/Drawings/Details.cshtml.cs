using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PDFHub.Data;
using PDFHub.Services;

namespace PDFHub.Pages.Drawings;

public class DetailsModel(AppDbContext db) : PageModel
{
    public Drawing Drawing { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string code)
    {
        var normalized = PdfCodeRules.Normalize(code);
        if (normalized != code)
            return RedirectToPage(new { code = normalized });

        var drawing = await db.Drawings.AsNoTracking().Include(d => d.Section).FirstOrDefaultAsync(d => d.PdfCode == normalized);
        if (drawing is null)
            return NotFound();

        Drawing = drawing;
        return Page();
    }
}
