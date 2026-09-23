using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PDFHub.Data;
using PDFHub.Services;

namespace PDFHub.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public DrawingFilter Filter { get; set; } = new();

    public List<Drawing> Items { get; private set; } = [];
    public int Total { get; private set; }
    public int PageCount { get; private set; }
    public List<Section> Sections { get; private set; } = [];
    public List<int> Years { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Sections = await db.Sections.AsNoTracking().OrderBy(s => s.Code).ToListAsync();
        Years = await db.Drawings.Select(d => d.InputDate.Year).Distinct().OrderByDescending(y => y).ToListAsync();

        var query = DrawingSearch.Apply(db.Drawings.AsNoTracking().Include(d => d.Section), Filter);
        Total = await query.CountAsync();
        PageCount = Math.Max(1, (int)Math.Ceiling(Total / (double)Filter.PageSize));
        Filter.Page = Math.Clamp(Filter.Page, 1, PageCount);

        Items = await DrawingSearch.Sort(query, Filter)
            .Skip((Filter.Page - 1) * Filter.PageSize)
            .Take(Filter.PageSize)
            .ToListAsync();
    }

    /// <summary>Downloads every drawing matching the current filter (not just the visible page).</summary>
    public async Task<IActionResult> OnGetExportAsync()
    {
        var query = DrawingSearch.Apply(db.Drawings.AsNoTracking().Include(d => d.Section), Filter);
        var items = await DrawingSearch.Sort(query, Filter).ToListAsync();
        var baseUrl = $"{Request.Scheme}://{Request.Host}{Request.PathBase}";
        var title = $"DATA DRAWING — PDFHub export {DateTime.Now:dd/MM/yyyy HH:mm}";

        var bytes = ExcelExporter.Build(items, title, d => $"{baseUrl}/pdf/{d.PdfCode}.pdf");
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"PDFHub-{DateTime.Now:yyyyMMdd-HHmm}.xlsx");
    }
}
