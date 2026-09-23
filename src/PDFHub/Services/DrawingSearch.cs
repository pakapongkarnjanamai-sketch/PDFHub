using Microsoft.EntityFrameworkCore;
using PDFHub.Data;

namespace PDFHub.Services;

/// <summary>Search/filter/sort state of the drawing list, bound from the query string.</summary>
public class DrawingFilter
{
    public const string DefaultSort = "date";
    public const string DefaultDir = "desc";
    public const int DefaultSize = 50;

    public static readonly string[] SortKeys = ["code", "section", "part", "drawing", "material", "price", "date", "quo", "po"];

    public string? Q { get; set; }
    public string? Section { get; set; }
    public int? Year { get; set; }
    /// <summary>"yes" or "no"</summary>
    public string? Po { get; set; }
    /// <summary>"has" or "missing"</summary>
    public string? Pdf { get; set; }
    public string Sort { get; set; } = DefaultSort;
    public string Dir { get; set; } = DefaultDir;
    public int Page { get; set; } = 1;
    public int Size { get; set; } = DefaultSize;

    public string SortKey => SortKeys.Contains(Sort) ? Sort : DefaultSort;
    public bool Desc => Dir == "desc";
    public int PageSize => Math.Clamp(Size, 10, 500);

    public bool IsFiltered =>
        !string.IsNullOrWhiteSpace(Q) || !string.IsNullOrEmpty(Section) || Year is not null ||
        !string.IsNullOrEmpty(Po) || !string.IsNullOrEmpty(Pdf);

    public Dictionary<string, string> ToRoute(Action<DrawingFilter>? change = null)
    {
        var f = (DrawingFilter)MemberwiseClone();
        change?.Invoke(f);

        var route = new Dictionary<string, string>();
        if (!string.IsNullOrWhiteSpace(f.Q)) route["q"] = f.Q.Trim();
        if (!string.IsNullOrEmpty(f.Section)) route["section"] = f.Section;
        if (f.Year is { } year) route["year"] = year.ToString();
        if (!string.IsNullOrEmpty(f.Po)) route["po"] = f.Po;
        if (!string.IsNullOrEmpty(f.Pdf)) route["pdf"] = f.Pdf;
        if (f.Sort != DefaultSort || f.Dir != DefaultDir) { route["sort"] = f.Sort; route["dir"] = f.Dir; }
        if (f.Page > 1) route["page"] = f.Page.ToString();
        if (f.Size != DefaultSize) route["size"] = f.Size.ToString();
        return route;
    }

    /// <summary>Route for a column header: first click sorts that column, the next click flips the direction.</summary>
    public Dictionary<string, string> SortRoute(string key) => ToRoute(f =>
    {
        f.Dir = f.SortKey == key ? (f.Desc ? "asc" : "desc") : key is "date" or "price" ? "desc" : "asc";
        f.Sort = key;
        f.Page = 1;
    });

    public Dictionary<string, string> PageRoute(int page) => ToRoute(f => f.Page = page);
}

public static class DrawingSearch
{
    public static IQueryable<Drawing> Apply(IQueryable<Drawing> query, DrawingFilter f)
    {
        // Every word must match somewhere, so "SUS304 plate" narrows down instead of widening.
        foreach (var term in (f.Q ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var p = "%" + EscapeLike(term) + "%";
            query = query.Where(d =>
                EF.Functions.Like(d.PdfCode, p, "\\") ||
                EF.Functions.Like(d.PartName, p, "\\") ||
                EF.Functions.Like(d.DrawingNo!, p, "\\") ||
                EF.Functions.Like(d.Material!, p, "\\") ||
                EF.Functions.Like(d.QuoNo!, p, "\\") ||
                EF.Functions.Like(d.Remark!, p, "\\") ||
                EF.Functions.Like(d.Section.Name, p, "\\"));
        }

        if (!string.IsNullOrEmpty(f.Section))
            query = query.Where(d => d.Section.Code == f.Section);

        if (f.Year is { } year)
        {
            var from = new DateOnly(year, 1, 1);
            var to = from.AddYears(1);
            query = query.Where(d => d.InputDate >= from && d.InputDate < to);
        }

        query = f.Po switch
        {
            "yes" => query.Where(d => d.HasPo),
            "no" => query.Where(d => !d.HasPo),
            _ => query,
        };

        query = f.Pdf switch
        {
            "has" => query.Where(d => d.PdfFileName != null),
            "missing" => query.Where(d => d.PdfFileName == null),
            _ => query,
        };

        return query;
    }

    public static IQueryable<Drawing> Sort(IQueryable<Drawing> query, DrawingFilter f)
    {
        var sorted = (f.SortKey, f.Desc) switch
        {
            ("code", false) => query.OrderBy(d => d.PdfCode),
            ("code", true) => query.OrderByDescending(d => d.PdfCode),
            ("section", false) => query.OrderBy(d => d.Section.Name),
            ("section", true) => query.OrderByDescending(d => d.Section.Name),
            ("part", false) => query.OrderBy(d => d.PartName),
            ("part", true) => query.OrderByDescending(d => d.PartName),
            ("drawing", false) => query.OrderBy(d => d.DrawingNo),
            ("drawing", true) => query.OrderByDescending(d => d.DrawingNo),
            ("material", false) => query.OrderBy(d => d.Material),
            ("material", true) => query.OrderByDescending(d => d.Material),
            ("price", false) => query.OrderBy(d => d.Price),
            ("price", true) => query.OrderByDescending(d => d.Price),
            ("quo", false) => query.OrderBy(d => d.QuoNo),
            ("quo", true) => query.OrderByDescending(d => d.QuoNo),
            ("po", false) => query.OrderBy(d => d.HasPo),
            ("po", true) => query.OrderByDescending(d => d.HasPo),
            ("date", false) => query.OrderBy(d => d.InputDate),
            _ => query.OrderByDescending(d => d.InputDate),
        };
        return f.Desc ? sorted.ThenByDescending(d => d.PdfCode) : sorted.ThenBy(d => d.PdfCode);
    }

    private static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
}
