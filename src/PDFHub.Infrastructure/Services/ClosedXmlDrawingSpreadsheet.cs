using System.Globalization;
using ClosedXML.Excel;
using PDFHub.Application.Abstractions;
using PDFHub.Domain.DTOs;

namespace PDFHub.Infrastructure.Services;

/// <summary>
/// The column layout of the original "NMB-2026" sheet. Export writes the same headers, so an exported
/// file can be edited in Excel and imported back.
/// </summary>
public sealed class ClosedXmlDrawingSpreadsheet : IDrawingSpreadsheet
{
    private enum Column { Code, PartName, DrawingNo, Material, Price, InputDate, QuoNo, Remark, PoNo, LegacyPoTick }

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
        ["pono"] = Column.PoNo, ["ponumber"] = Column.PoNo, ["po"] = Column.PoNo,
        ["haveapo"] = Column.LegacyPoTick, ["havepo"] = Column.LegacyPoTick,
    };

    private static readonly string[] DateFormats = ["d/M/yyyy", "dd/MM/yyyy", "yyyy-MM-dd", "d-M-yyyy", "d.M.yyyy"];

    private static readonly string[] ExportHeaders =
        ["No.", "PdfCodeS", "Sections", "PartName", "DrawingNo", "MatS", "Price", "Drw.PdfLink", "InputDate", "QuoNo.", "Remark", "PO No."];

    private static readonly double[] ExportWidths = [7, 11, 13, 30, 20, 12, 11, 16, 12, 14, 32, 16];

    public IReadOnlyList<SpreadsheetSheet> Read(Stream xlsx)
    {
        using var workbook = new XLWorkbook(xlsx);
        var sheets = new List<SpreadsheetSheet>();

        foreach (var sheet in workbook.Worksheets)
        {
            if (FindColumns(sheet) is not { } found)
                continue;
            var (headerRow, columns) = found;

            var rows = new List<SpreadsheetRow>();
            var lastRow = sheet.LastRowUsed()?.RowNumber() ?? headerRow;
            for (var r = headerRow + 1; r <= lastRow; r++)
            {
                IXLCell? Cell(Column c) => columns.TryGetValue(c, out var col) ? sheet.Cell(r, col) : null;
                string? Text(Column c) => Cell(c) is { } cell ? ReadText(cell) : null;

                var code = Text(Column.Code);
                var partName = Text(Column.PartName);
                if (code is null && partName is null && Text(Column.DrawingNo) is null)
                    continue;

                var priceCell = Cell(Column.Price);
                var dateCell = Cell(Column.InputDate);
                rows.Add(new SpreadsheetRow(
                    RowNumber: r,
                    Code: code,
                    PartName: partName,
                    DrawingNo: Text(Column.DrawingNo),
                    Material: Text(Column.Material),
                    PriceText: priceCell is null ? null : ReadText(priceCell),
                    Price: priceCell is null ? null : ReadNumber(priceCell),
                    InputDateText: dateCell is null ? null : ReadText(dateCell),
                    InputDate: dateCell is null ? null : ReadDate(dateCell),
                    QuoNo: Text(Column.QuoNo),
                    Remark: Text(Column.Remark),
                    PoNo: Text(Column.PoNo),
                    LegacyPoTick: Text(Column.LegacyPoTick)));
            }
            sheets.Add(new SpreadsheetSheet(sheet.Name, rows));
        }
        return sheets;
    }

    public byte[] Write(IReadOnlyList<DrawingListItemDto> drawings, string title, Func<string, string> pdfUrl)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Drawings");

        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 1, ExportHeaders.Length).Merge().Style.Font.SetBold().Font.SetFontSize(14);
        for (var c = 0; c < ExportHeaders.Length; c++)
        {
            sheet.Cell(2, c + 1).Value = ExportHeaders[c];
            sheet.Column(c + 1).Width = ExportWidths[c];
        }
        sheet.Range(2, 1, 2, ExportHeaders.Length).Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DDE3EC"));

        var row = 3;
        foreach (var d in drawings)
        {
            sheet.Cell(row, 1).Value = row - 2;
            sheet.Cell(row, 2).Value = d.PdfCode;
            sheet.Cell(row, 3).Value = d.SectionName;
            sheet.Cell(row, 4).Value = d.PartName;
            sheet.Cell(row, 5).Value = d.DrawingNo;
            sheet.Cell(row, 6).Value = d.Material;
            if (d.Price is { } price)
                sheet.Cell(row, 7).Value = price;
            if (d.HasPdf)
            {
                var link = sheet.Cell(row, 8);
                link.Value = d.PdfCode + ".pdf";
                link.SetHyperlink(new XLHyperlink(new Uri(pdfUrl(d.PdfCode))));
            }
            sheet.Cell(row, 9).Value = d.InputDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 10).Value = d.QuoNo;
            sheet.Cell(row, 11).Value = d.Remark;
            sheet.Cell(row, 12).Value = d.PoNo;
            row++;
        }

        sheet.Column(7).Style.NumberFormat.Format = "#,##0.##";
        sheet.Column(9).Style.DateFormat.Format = "dd/mm/yyyy";
        sheet.Range(2, 1, Math.Max(row - 1, 2), ExportHeaders.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(2);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
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
            if (!Enumerable.Range(1, lastColumn).Any(c => HeaderKey(sheet.Cell(r, c)) is "pdfcodes" or "pdfcode"))
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
        if (cell.Value.IsNumber)
            return (decimal)cell.Value.GetNumber();
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
