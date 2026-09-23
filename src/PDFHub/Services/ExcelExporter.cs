using ClosedXML.Excel;
using PDFHub.Data;

namespace PDFHub.Services;

/// <summary>Writes drawings in the same column layout as the original sheet, so an export can be edited and imported back.</summary>
public static class ExcelExporter
{
    private static readonly string[] Headers =
        ["No.", "PdfCodeS", "Sections", "PartName", "DrawingNo", "MatS", "Price", "Drw.PdfLink", "InputDate", "QuoNo.", "Remark", "Have a PO"];

    private static readonly double[] Widths = [7, 11, 13, 30, 20, 12, 11, 16, 12, 14, 32, 10];

    public static byte[] Build(IReadOnlyList<Drawing> drawings, string title, Func<Drawing, string> pdfUrl)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Drawings");

        sheet.Cell(1, 1).Value = title;
        sheet.Range(1, 1, 1, Headers.Length).Merge().Style.Font.SetBold().Font.SetFontSize(14);

        for (var c = 0; c < Headers.Length; c++)
        {
            sheet.Cell(2, c + 1).Value = Headers[c];
            sheet.Column(c + 1).Width = Widths[c];
        }
        var header = sheet.Range(2, 1, 2, Headers.Length);
        header.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DDE3EC"));

        var row = 3;
        foreach (var d in drawings)
        {
            sheet.Cell(row, 1).Value = row - 2;
            sheet.Cell(row, 2).Value = d.PdfCode;
            sheet.Cell(row, 3).Value = d.Section.Name;
            sheet.Cell(row, 4).Value = d.PartName;
            sheet.Cell(row, 5).Value = d.DrawingNo;
            sheet.Cell(row, 6).Value = d.Material;
            if (d.Price is { } price)
                sheet.Cell(row, 7).Value = price;
            if (d.PdfFileName is not null)
            {
                var link = sheet.Cell(row, 8);
                link.Value = d.PdfCode + ".pdf";
                link.SetHyperlink(new XLHyperlink(new Uri(pdfUrl(d))));
            }
            sheet.Cell(row, 9).Value = d.InputDate.ToDateTime(TimeOnly.MinValue);
            sheet.Cell(row, 10).Value = d.QuoNo;
            sheet.Cell(row, 11).Value = d.Remark;
            if (d.HasPo)
                sheet.Cell(row, 12).Value = "✓";
            row++;
        }

        sheet.Column(7).Style.NumberFormat.Format = "#,##0.##";
        sheet.Column(9).Style.DateFormat.Format = "dd/mm/yyyy";
        sheet.Column(12).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        sheet.Range(2, 1, Math.Max(row - 1, 2), Headers.Length).SetAutoFilter();
        sheet.SheetView.FreezeRows(2);

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }
}
