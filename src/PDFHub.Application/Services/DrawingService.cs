using Microsoft.EntityFrameworkCore;
using PDFHub.Application.Abstractions;
using PDFHub.Application.Exceptions;
using PDFHub.Domain;
using PDFHub.Domain.DTOs;
using PDFHub.Domain.Entities;

namespace PDFHub.Application.Services;

public interface IDrawingService
{
    Task<DrawingListResultDto> ListAsync(DrawingListQuery query, CancellationToken ct = default);
    Task<byte[]> ExportAsync(DrawingListQuery query, string title, Func<string, string> pdfUrl, CancellationToken ct = default);
    Task<DrawingDetailDto> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<DrawingLookupsDto> GetLookupsAsync(CancellationToken ct = default);
    Task<PdfCodeCheckDto> CheckCodeAsync(string? code, int? drawingId, CancellationToken ct = default);
    Task<DrawingDetailDto> CreateAsync(CreateDrawingDto dto, CancellationToken ct = default);
    Task<DrawingDetailDto> UpdateAsync(int id, UpdateDrawingDto dto, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
    /// <param name="content">Must be seekable: the PDF signature is checked before the file is stored.</param>
    Task<DrawingDetailDto> UploadPdfAsync(int id, Stream content, long length, CancellationToken ct = default);
    /// <summary>Bulk upload: the file name must be the PdfCode (NB-06618.pdf), as in the old Excel hyperlinks.</summary>
    Task<PdfUploadResultDto> UploadPdfByFileNameAsync(string fileName, Stream content, long length, bool overwrite, CancellationToken ct = default);
    /// <summary>Null when the drawing does not exist, has no PDF, or the file is missing on disk.</summary>
    Task<PdfFileDto?> GetPdfAsync(string fileNameOrCode, CancellationToken ct = default);
}

public sealed class DrawingService(IUnitOfWork uow, IPdfStorage storage, IDrawingSpreadsheet spreadsheet, IDateTime clock) : IDrawingService
{
    private IRepository<Drawing> Drawings => uow.Repository<Drawing>();
    private IRepository<Section> Sections => uow.Repository<Section>();

    public async Task<DrawingListResultDto> ListAsync(DrawingListQuery query, CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 10, 500);
        var filtered = Filter(Drawings.GetAll().AsNoTracking(), query);
        var filteredCount = await filtered.CountAsync(ct);
        var pageCount = Math.Max(1, (int)Math.Ceiling(filteredCount / (double)pageSize));
        var page = Math.Clamp(query.Page, 1, pageCount);

        return new DrawingListResultDto
        {
            TotalCount = await Drawings.GetAll().CountAsync(ct),
            FilteredCount = filteredCount,
            Page = page,
            PageSize = pageSize,
            Items = await ToListItems(Sort(filtered, query).Skip((page - 1) * pageSize).Take(pageSize)).ToListAsync(ct),
            // Built from the unfiltered set so choosing one filter never empties the other dropdowns.
            FilterOptions = new DrawingFilterOptionsDto
            {
                Sections = await Sections.GetAll().AsNoTracking().OrderBy(s => s.Code)
                    .Select(s => new SectionOptionDto(s.Code, s.Name)).ToListAsync(ct),
                Years = await Drawings.GetAll().Select(d => d.InputDate.Year).Distinct()
                    .OrderByDescending(y => y).ToListAsync(ct),
            },
        };
    }

    public async Task<byte[]> ExportAsync(DrawingListQuery query, string title, Func<string, string> pdfUrl, CancellationToken ct = default)
    {
        // Same filter and order as the list, every page.
        var rows = await ToListItems(Sort(Filter(Drawings.GetAll().AsNoTracking(), query), query)).ToListAsync(ct);
        return spreadsheet.Write(rows, title, pdfUrl);
    }

    public async Task<DrawingDetailDto> GetByCodeAsync(string code, CancellationToken ct = default)
    {
        var normalized = PdfCodeRules.Normalize(code);
        var detail = await ToDetails(Drawings.GetAll().AsNoTracking().Where(d => d.PdfCode == normalized)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException($"ไม่พบ Drawing {normalized}");
        detail.NextCode = PdfCodeRules.Next(detail.PdfCode);
        return detail;
    }

    public async Task<DrawingLookupsDto> GetLookupsAsync(CancellationToken ct = default)
    {
        var sections = await Sections.GetAll().AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Code)
            .Select(s => new SectionOptionDto(s.Code, s.Name)).ToListAsync(ct);
        var materials = await Drawings.GetAll().Where(d => d.Material != null).Select(d => d.Material!)
            .Distinct().OrderBy(m => m).ToListAsync(ct);
        return new DrawingLookupsDto(sections, materials);
    }

    public async Task<PdfCodeCheckDto> CheckCodeAsync(string? code, int? drawingId, CancellationToken ct = default)
    {
        var (normalized, error, section) = await ValidateCodeAsync(code, drawingId, ct);
        return new PdfCodeCheckDto(normalized, error is null, error, section?.Name);
    }

    public async Task<DrawingDetailDto> CreateAsync(CreateDrawingDto dto, CancellationToken ct = default)
    {
        var section = await ValidateAsync(dto, null, ct);
        var drawing = Drawings.New();
        Apply(dto, drawing, section);
        await Drawings.AddAsync(drawing);
        await SaveAsync(drawing.PdfCode, ct);
        return await GetByCodeAsync(drawing.PdfCode, ct);
    }

    public async Task<DrawingDetailDto> UpdateAsync(int id, UpdateDrawingDto dto, CancellationToken ct = default)
    {
        var drawing = await FindAsync(id);
        var section = await ValidateAsync(dto, id, ct);
        var oldCode = drawing.PdfCode;
        Apply(dto, drawing, section);

        if (oldCode != drawing.PdfCode && drawing.PdfFileName is not null)
            drawing.PdfFileName = storage.Rename(drawing.PdfFileName, drawing.PdfCode);

        await SaveAsync(drawing.PdfCode, ct);
        return await GetByCodeAsync(drawing.PdfCode, ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var drawing = await FindAsync(id);
        if (drawing.PdfFileName is not null)
            storage.Archive(drawing.PdfFileName, drawing.PdfCode, "deleted");
        await Drawings.RemoveAsync(drawing);
        await uow.CommitAsync();
    }

    public async Task<DrawingDetailDto> UploadPdfAsync(int id, Stream content, long length, CancellationToken ct = default)
    {
        var drawing = await FindAsync(id);
        if (await CheckPdfAsync(content, length, ct) is { } error)
            throw ValidationException.For("File", error);

        await StorePdfAsync(drawing, content, length, ct);
        return await GetByCodeAsync(drawing.PdfCode, ct);
    }

    public async Task<PdfUploadResultDto> UploadPdfByFileNameAsync(string fileName, Stream content, long length, bool overwrite, CancellationToken ct = default)
    {
        var code = PdfCodeRules.Normalize(Path.GetFileNameWithoutExtension(fileName));
        if (!fileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            return new("skip", code, "ไม่ใช่ไฟล์ .pdf");
        if (PdfCodeRules.FormatError(code) is not null)
            return new("error", code, "ชื่อไฟล์ไม่ใช่ PdfCode (ต้องเป็นแบบ NB-06618.pdf)");

        var drawing = await Drawings.GetAll().FirstOrDefaultAsync(d => d.PdfCode == code, ct);
        if (drawing is null)
            return new("error", code, "ไม่พบ PdfCode นี้ในฐานข้อมูล (ต้องเพิ่มข้อมูลหรือนำเข้า Excel ก่อน)");
        if (drawing.PdfFileName is not null && !overwrite)
            return new("skip", code, "มีไฟล์อยู่แล้ว (ข้าม)");
        if (await CheckPdfAsync(content, length, ct) is { } error)
            return new("error", code, error);

        var replaced = drawing.PdfFileName is not null;
        await StorePdfAsync(drawing, content, length, ct);
        return new("ok", code, replaced ? "แทนที่ไฟล์เดิมแล้ว" : "อัปโหลดแล้ว");
    }

    public async Task<PdfFileDto?> GetPdfAsync(string fileNameOrCode, CancellationToken ct = default)
    {
        var code = PdfCodeRules.Normalize(Path.GetFileNameWithoutExtension(fileNameOrCode));
        var relative = await Drawings.GetAll().AsNoTracking().Where(d => d.PdfCode == code)
            .Select(d => d.PdfFileName).FirstOrDefaultAsync(ct);
        if (relative is null)
            return null;

        var path = storage.FullPath(relative);
        return File.Exists(path) ? new PdfFileDto(path, $"{code}.pdf") : null;
    }

    // ---------------------------------------------------------------- query

    private static IQueryable<Drawing> Filter(IQueryable<Drawing> query, DrawingListQuery f)
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
                EF.Functions.Like(d.PoNo!, p, "\\") ||
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
            "yes" => query.Where(d => d.PoNo != null),
            "no" => query.Where(d => d.PoNo == null),
            _ => query,
        };

        return f.Pdf switch
        {
            "has" => query.Where(d => d.PdfFileName != null),
            "missing" => query.Where(d => d.PdfFileName == null),
            _ => query,
        };
    }

    private static IQueryable<Drawing> Sort(IQueryable<Drawing> query, DrawingListQuery f)
    {
        var sorted = (f.SortKey, f.Descending) switch
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
            // An unpriced drawing is not a free one: blanks sort last in both directions.
            ("price", false) => query.OrderBy(d => d.Price == null).ThenBy(d => d.Price),
            ("price", true) => query.OrderBy(d => d.Price == null).ThenByDescending(d => d.Price),
            ("quo", false) => query.OrderBy(d => d.QuoNo),
            ("quo", true) => query.OrderByDescending(d => d.QuoNo),
            ("po", false) => query.OrderBy(d => d.PoNo == null).ThenBy(d => d.PoNo),
            ("po", true) => query.OrderBy(d => d.PoNo == null).ThenByDescending(d => d.PoNo),
            ("date", false) => query.OrderBy(d => d.InputDate),
            _ => query.OrderByDescending(d => d.InputDate),
        };
        // PdfCode is unique, so paging never reshuffles rows that tie on the sort column.
        return f.Descending ? sorted.ThenByDescending(d => d.PdfCode) : sorted.ThenBy(d => d.PdfCode);
    }

    private static IQueryable<DrawingListItemDto> ToListItems(IQueryable<Drawing> query) => query.Select(d => new DrawingListItemDto
    {
        Id = d.Id,
        PdfCode = d.PdfCode,
        SectionCode = d.Section.Code,
        SectionName = d.Section.Name,
        PartName = d.PartName,
        DrawingNo = d.DrawingNo ?? "",
        Material = d.Material ?? "",
        Price = d.Price,
        InputDate = d.InputDate,
        QuoNo = d.QuoNo ?? "",
        Remark = d.Remark ?? "",
        PoNo = d.PoNo ?? "",
        HasPdf = d.PdfFileName != null,
    });

    private static IQueryable<DrawingDetailDto> ToDetails(IQueryable<Drawing> query) => query.Select(d => new DrawingDetailDto
    {
        Id = d.Id,
        PdfCode = d.PdfCode,
        SectionCode = d.Section.Code,
        SectionName = d.Section.Name,
        PartName = d.PartName,
        DrawingNo = d.DrawingNo ?? "",
        Material = d.Material ?? "",
        Price = d.Price,
        InputDate = d.InputDate,
        QuoNo = d.QuoNo ?? "",
        Remark = d.Remark ?? "",
        PoNo = d.PoNo ?? "",
        HasPdf = d.PdfFileName != null,
        PdfSize = d.PdfSize,
        PdfUploadedAt = d.PdfUploadedAt,
        CreatedAt = d.CreatedAt,
        CreatedBy = d.CreatedBy ?? "",
        UpdatedAt = d.UpdatedAt,
        UpdatedBy = d.UpdatedBy ?? "",
    });

    private static string EscapeLike(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    // ---------------------------------------------------------------- write

    private async Task<Drawing> FindAsync(int id) =>
        await Drawings.GetByIdAsync(id) ?? throw new KeyNotFoundException($"ไม่พบ Drawing รหัส {id}");

    private async Task<(string Code, string? Error, Section? Section)> ValidateCodeAsync(string? raw, int? drawingId, CancellationToken ct)
    {
        var code = PdfCodeRules.Normalize(raw);
        if (PdfCodeRules.FormatError(code) is { } formatError)
            return (code, formatError, null);

        var prefix = code[..2];
        var section = await Sections.GetAll().FirstOrDefaultAsync(s => s.Code == prefix, ct);
        if (section is null)
            return (code, PdfCodeRules.UnknownSection(prefix), null);

        var current = drawingId is null
            ? null
            : await Drawings.GetAll().Where(d => d.Id == drawingId).Select(d => d.PdfCode).FirstOrDefaultAsync(ct);

        // A drawing may keep its code after its section is retired, but no new codes go into a retired section.
        if (!section.IsActive && current != code)
            return (code, $"Section {prefix} ถูกปิดใช้งานแล้ว", section);

        if (current != code && await Drawings.GetAll().AnyAsync(d => d.PdfCode == code, ct))
            return (code, $"PdfCode {code} มีอยู่ในระบบแล้ว", section);

        return (code, null, section);
    }

    private async Task<Section> ValidateAsync(CreateDrawingDto dto, int? drawingId, CancellationToken ct)
    {
        var errors = new ValidationErrors();
        var (code, codeError, section) = await ValidateCodeAsync(dto.PdfCode, drawingId, ct);
        dto.PdfCode = code;
        if (codeError is not null) errors.Add(nameof(dto.PdfCode), codeError);

        if (string.IsNullOrWhiteSpace(dto.PartName)) errors.Add(nameof(dto.PartName), "กรุณากรอก Part Name");
        else if (dto.PartName.Trim().Length > 200) errors.Add(nameof(dto.PartName), "Part Name ยาวเกิน 200 ตัวอักษร");
        if (dto.DrawingNo?.Trim().Length > 100) errors.Add(nameof(dto.DrawingNo), "Drawing No. ยาวเกิน 100 ตัวอักษร");
        if (dto.Material?.Trim().Length > 100) errors.Add(nameof(dto.Material), "Material ยาวเกิน 100 ตัวอักษร");
        if (dto.Price is < 0) errors.Add(nameof(dto.Price), "ราคาต้องเป็นตัวเลขตั้งแต่ 0 ขึ้นไป");
        if (dto.InputDate is null) errors.Add(nameof(dto.InputDate), "กรุณาระบุวันที่");
        if (dto.QuoNo?.Trim().Length > 50) errors.Add(nameof(dto.QuoNo), "Quotation No. ยาวเกิน 50 ตัวอักษร");
        if (dto.PoNo?.Trim().Length > 50) errors.Add(nameof(dto.PoNo), "PO No. ยาวเกิน 50 ตัวอักษร");
        if (dto.Remark?.Trim().Length > 1000) errors.Add(nameof(dto.Remark), "Remark ยาวเกิน 1000 ตัวอักษร");

        errors.ThrowIfAny();
        return section!;
    }

    private static void Apply(CreateDrawingDto dto, Drawing d, Section section)
    {
        d.PdfCode = dto.PdfCode!;
        d.SectionId = section.Id;
        d.PartName = dto.PartName!.Trim();
        d.DrawingNo = Clean(dto.DrawingNo);
        d.Material = Clean(dto.Material);
        d.Price = dto.Price;
        d.InputDate = dto.InputDate!.Value;
        d.QuoNo = Clean(dto.QuoNo);
        d.Remark = Clean(dto.Remark);
        d.PoNo = Clean(dto.PoNo);
    }

    private static string? Clean(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private async Task SaveAsync(string code, CancellationToken ct)
    {
        try
        {
            await uow.CommitAsync();
        }
        catch (DbUpdateException)
        {
            // Another user saved the same code between the check and this save.
            throw ValidationException.For(nameof(CreateDrawingDto.PdfCode), $"PdfCode {code} มีอยู่ในระบบแล้ว");
        }
    }

    private async Task<string?> CheckPdfAsync(Stream content, long length, CancellationToken ct)
    {
        if (length == 0)
            return "ไฟล์ว่างเปล่า";
        if (length > storage.MaxFileBytes)
            return $"ไฟล์ใหญ่เกิน {storage.MaxFileBytes / 1024 / 1024} MB";
        var isPdf = await PdfSignature.IsPdfAsync(content, ct);
        content.Position = 0;
        return isPdf ? null : "ไฟล์นี้ไม่ใช่ PDF";
    }

    private async Task StorePdfAsync(Drawing drawing, Stream content, long length, CancellationToken ct)
    {
        drawing.PdfFileName = await storage.SaveAsync(drawing.PdfCode, content, ct);
        drawing.PdfSize = length;
        drawing.PdfUploadedAt = clock.Now;
        await uow.CommitAsync();
    }
}
