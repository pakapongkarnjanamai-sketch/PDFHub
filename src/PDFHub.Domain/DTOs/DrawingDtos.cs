namespace PDFHub.Domain.DTOs;

/// <summary>
/// Query string of GET /api/drawings. Every control on the list page maps to one of these;
/// the browser never filters or sorts rows itself.
/// </summary>
public class DrawingListQuery
{
    public const string DefaultSort = "date";
    public static readonly string[] SortKeys = ["code", "section", "part", "drawing", "material", "price", "date", "quo", "po"];

    public string? Q { get; set; }
    public string? Section { get; set; }
    public int? Year { get; set; }
    /// <summary>"yes" | "no" | empty</summary>
    public string? Po { get; set; }
    /// <summary>"has" | "missing" | empty</summary>
    public string? Pdf { get; set; }
    public string? Sort { get; set; }
    /// <summary>"asc" | "desc"; defaults to desc for date/price and asc otherwise.</summary>
    public string? Dir { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    public string SortKey => SortKeys.Contains(Sort) ? Sort! : DefaultSort;
    public bool Descending => Dir is null ? SortKey is "date" or "price" : Dir == "desc";
}

public class DrawingListItemDto
{
    public int Id { get; set; }
    public string PdfCode { get; set; } = string.Empty;
    public string SectionCode { get; set; } = string.Empty;
    public string SectionName { get; set; } = string.Empty;
    public string PartName { get; set; } = string.Empty;
    public string DrawingNo { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public decimal? Price { get; set; }
    public DateOnly InputDate { get; set; }
    public string QuoNo { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public bool HasPo { get; set; }
    public bool HasPdf { get; set; }
}

public class DrawingDetailDto : DrawingListItemDto
{
    public long? PdfSize { get; set; }
    public DateTime? PdfUploadedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
    /// <summary>The following sticker code, used by "save and add next".</summary>
    public string? NextCode { get; set; }
}

public record SectionOptionDto(string Code, string Name);

public class DrawingFilterOptionsDto
{
    public IReadOnlyList<SectionOptionDto> Sections { get; set; } = [];
    public IReadOnlyList<int> Years { get; set; } = [];
}

public class DrawingListResultDto
{
    /// <summary>All drawings, before search and filters.</summary>
    public int TotalCount { get; set; }
    /// <summary>After search and filters — also the number of rows an export of this query contains.</summary>
    public int FilteredCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public IReadOnlyList<DrawingListItemDto> Items { get; set; } = [];
    public DrawingFilterOptionsDto FilterOptions { get; set; } = new();
}

public class CreateDrawingDto
{
    public string? PdfCode { get; set; }
    public string? PartName { get; set; }
    public string? DrawingNo { get; set; }
    public string? Material { get; set; }
    public decimal? Price { get; set; }
    public DateOnly? InputDate { get; set; }
    public string? QuoNo { get; set; }
    public string? Remark { get; set; }
    public bool HasPo { get; set; }
}

public class UpdateDrawingDto : CreateDrawingDto;

public record PdfCodeCheckDto(string Code, bool Valid, string? Error, string? SectionName);

/// <summary>Result of one file in the bulk PDF upload. Status: "ok" | "skip" | "error".</summary>
public record PdfUploadResultDto(string Status, string Code, string Message);

public record DrawingLookupsDto(IReadOnlyList<SectionOptionDto> Sections, IReadOnlyList<string> Materials);

/// <summary>A stored PDF ready to stream.</summary>
public record PdfFileDto(string FullPath, string FileName);
