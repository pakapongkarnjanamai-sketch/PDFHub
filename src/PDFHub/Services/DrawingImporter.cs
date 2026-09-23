using System.Globalization;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using PDFHub.Data;

namespace PDFHub.Services;

public enum ImportMode
{
    /// <summary>Only add codes that are not in the database yet.</summary>
    SkipExisting,
    /// <summary>Overwrite existing drawings with the values in the file.</summary>
    UpdateExisting,
}

public sealed record ImportIssue(string Sheet, int Row, string? Code, string Message);

public sealed class ImportResult
{
    public bool DryRun { get; init; }
    public List<string> Sheets { get; } = [];
    public int RowsRead { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<ImportIssue> Errors { get; } = [];
    public List<ImportIssue> Warnings { get; } = [];
}

/// <summary>
/// Reads drawing rows from an .xlsx laid out like the original "NMB-2026" sheet (or an export from this app).
/// Every sheet with a "PdfCodeS" header is imported; the Sections and Drw.PdfLink columns are ignored
/// because both are derived from the PdfCode.
/// </summary>
public sealed class DrawingImporter(AppDbContext db)
{
    private enum Column { Code, PartName, DrawingNo, Material, Price, InputDate, QuoNo, Remark, HasPo }

    private static readonly Dictionary<string, Column> HeaderNames = new()
    {
        ["pdfcodes"] = Column.Code, ["pdfcode"] = Column.Code,
        ["partname"] = Column.PartName,
        ["drawingno"] = Column.DrawingNo, ["drawingnumber"] = Column.DrawingNo,
        ["mats"] = Column.Material, ["material"] = Column.Material,
        ["price"] = Column.Price,
        ["inputdate"] = Column.InputDate,
        ["quono"] = Column.QuoNo, ["quotationno"] = Column.QuoNo,
        ["remark"] = Column.Remark, ["remarks"] = Column.Remark,
        ["haveapo"] = Column.HasPo, ["havepo"] = Column.HasPo, ["po"] = Column.HasPo,
    };

    private static readonly string[] FalseWords = ["0", "false", "no", "n", "-", "ไม่มี"];
    private static readonly string[] DateFormats = ["d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d-M-yyyy", "d.M.yyyy"];

    public async Task<ImportResult> ImportAsync(Stream xlsx, ImportMode mode, bool dryRun, string? userName, CancellationToken ct = default)
    {
        using var workbook = new XLWorkbook(xlsx);
        var sections = await db.Sections.ToDictionaryAsync(s => s.Code, ct);
        var existing = await db.Drawings.ToDictionaryAsync(d => d.PdfCode, ct);
        var seen = new HashSet<string>();
        var now = DateTime.Now;
        var result = new ImportResult { DryRun = dryRun };

        foreach (var sheet in workbook.Worksheets)
        {
            if (FindColumns(sheet) is not { } found)
                continue;
            var (headerRow, columns) = found;
            result.Sheets.Add(sheet.Name);

            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;
            for (var r = headerRow + 1; r <= lastRow; r++)
            {
                string? Text(Column c) => columns.TryGetValue(c, out var col) ? ReadText(sheet.Cell(r, col)) : null;
                IXLCell? Cell(Column c) => columns.TryGetValue(c, out var col) ? sheet.Cell(r, col) : null;

                var rawCode = Text(Column.Code);
                var partName = Text(Column.PartName);
                if (rawCode is null && partName is null && Text(Column.DrawingNo) is null)
                    continue;

                result.RowsRead++;
                void Error(string message) => result.Errors.Add(new(sheet.Name, r, rawCode, message));
                void Warn(string message) => result.Warnings.Add(new(sheet.Name, r, rawCode, message));

                var code = PdfCodeRules.Normalize(rawCode);
                if (PdfCodeRules.FormatError(code) is { } formatError) { Error(formatError); continue; }
                if (!sections.TryGetValue(code[..2], out var section)) { Error(PdfCodeRules.UnknownSection(code[..2])); continue; }
                if (!seen.Add(code)) { Error($"PdfCode {code} ซ้ำกันในไฟล์"); continue; }
                if (partName is null) { Error("ไม่มี PartName"); continue; }

                decimal? price = null;
                if (Cell(Column.Price) is { } priceCell && !priceCell.IsEmpty())
                {
                    price = ReadNumber(priceCell);
                    if (price is null) Warn($"อ่านราคา \"{ReadText(priceCell)}\" ไม่ได้ จึงเว้นว่างไว้");
                }

                var inputDate = Cell(Column.InputDate) is { } dateCell ? ReadDate(dateCell) : null;
                if (inputDate is null)
                {
                    inputDate = DateOnly.FromDateTime(now);
                    Warn("ไม่มี InputDate หรืออ่านวันที่ไม่ได้ จึงใช้วันที่นำเข้าแทน");
                }

                var hasPo = Text(Column.HasPo) is { } po && !FalseWords.Contains(po, StringComparer.OrdinalIgnoreCase);

                if (existing.TryGetValue(code, out var drawing))
                {
                    if (mode == ImportMode.SkipExisting) { result.Skipped++; continue; }
                    result.Updated++;
                }
                else
                {
                    drawing = new Drawing { PdfCode = code, CreatedAt = now, CreatedBy = userName };
                    existing[code] = drawing;
                    if (!dryRun) db.Drawings.Add(drawing);
                    result.Added++;
                }

                if (dryRun)
                    continue;
                drawing.SectionId = section.Id;
                drawing.PartName = partName;
                drawing.DrawingNo = Text(Column.DrawingNo);
                drawing.Material = Text(Column.Material);
                drawing.Price = price;
                drawing.InputDate = inputDate.Value;
                drawing.QuoNo = Text(Column.QuoNo);
                drawing.Remark = Text(Column.Remark);
                drawing.HasPo = hasPo;
                drawing.UpdatedAt = now;
                drawing.UpdatedBy = userName;
            }
        }

        if (result.Sheets.Count == 0)
            result.Errors.Add(new("-", 0, null, "ไม่พบหัวตารางที่มีคอลัมน์ PdfCodeS ในไฟล์นี้"));

        if (!dryRun)
            await db.SaveChangesAsync(ct);
        return result;
    }

    /// <summary>
    /// Finds the header row (the one containing "PdfCodeS") within the first 10 rows. The original sheet puts
    /// "Have a PO" one row above the other headers, so that row is searched as well.
    /// </summary>
    private static (int HeaderRow, Dictionary<Column, int> Columns)? FindColumns(IXLWorksheet sheet)
    {
        var lastColumn = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;
        for (var r = 1; r <= 10; r++)
        {
            var headers = Enumerable.Range(1, lastColumn).ToDictionary(c => c, c => HeaderKey(sheet.Cell(r, c)));
            if (!headers.Values.Any(h => h is "pdfcodes" or "pdfcode"))
                continue;

            var columns = new Dictionary<Column, int>();
            foreach (var row in r > 1 ? new[] { r, r - 1 } : [r])
                for (var c = 1; c <= lastColumn; c++)
                    if (HeaderNames.TryGetValue(HeaderKey(sheet.Cell(row, c)), out var column))
                        columns.TryAdd(column, c);
            return (r, columns);
        }
        return null;
    }

    private static string HeaderKey(IXLCell cell) =>
        new string((ReadText(cell) ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

    private static string? ReadText(IXLCell cell)
    {
        var value = cell.Value;
        var text = value.Type switch
        {
            XLDataType.Blank => null,
            XLDataType.Text => value.GetText(),
            XLDataType.Number => value.GetNumber().ToString(CultureInfo.InvariantCulture),
            XLDataType.Boolean => value.GetBoolean() ? "TRUE" : "FALSE",
            _ => cell.GetFormattedString(),
        };
        return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    }

    private static decimal? ReadNumber(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsNumber)
            return (decimal)value.GetNumber();
        var text = ReadText(cell)?.Replace(",", "").Replace("฿", "").Replace("บาท", "").Trim();
        return decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var n) ? n : null;
    }

    private static DateOnly? ReadDate(IXLCell cell)
    {
        var value = cell.Value;
        if (value.IsDateTime)
            return DateOnly.FromDateTime(value.GetDateTime());
        if (value.IsNumber)
            return DateOnly.FromDateTime(DateTime.FromOADate(value.GetNumber()));
        if (ReadText(cell) is not { } text ||
            !DateOnly.TryParseExact(text, DateFormats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;
        // Dates typed in the Thai Buddhist calendar (e.g. 08/08/2569).
        return date.Year > 2400 ? date.AddYears(-543) : date;
    }
}
