using System.Net;
using System.Text.Json;
using ClosedXML.Excel;

namespace PDFHub.Tests;

public class ImportTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    /// <summary>A sheet shaped like "NMB-2026": title row, headers on row 2, "Have a PO" on row 1.</summary>
    private static byte[] Workbook(params object?[][] rows)
    {
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("NMB-2026");
        ws.Cell(1, 1).Value = "DATA DRAWING MINEBEAR 2026";
        ws.Cell(1, 12).Value = "Have a PO";
        string[] headers = ["No.", "PdfCodeS", "Sections", "PartName", "DrawingNo", "MatS", "Price", "Drw.PdfLink", "InputDate", "QuoNo.", "Remark"];
        for (var c = 0; c < headers.Length; c++)
            ws.Cell(2, c + 1).Value = headers[c];
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++)
                ws.Cell(r + 3, c + 1).Value = XLCellValue.FromObject(rows[r][c]);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static async Task<JsonElement> ImportAsync(HttpClient client, byte[] file, bool dryRun, string mode = "SkipExisting")
    {
        var res = await client.UploadAsync(HttpMethod.Post, "/api/import/excel", "data.xlsx", file, ("mode", mode), ("dryRun", dryRun.ToString()));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.JsonAsync();
    }

    [Fact]
    public async Task Imports_rows_and_reports_problems()
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();
        var file = Workbook(
            [1, "NB-06618", "NMB", "CARRIER", "34281-44-0013A", "PEEK", 3900, null, new DateTime(2026, 8, 8), null, null, "ü"],
            [2, "nb-06619", "NMB", "BRG PUSHER", "T47691-01-0010", "ACRYLIC ", "1,200", null, "08/08/2569", null, null, null],
            [3, "NB-066", "NMB", "BAD CODE"],
            [4, "ZZ-00001", "?", "UNKNOWN SECTION"],
            [5, "NB-06618", "NMB", "DUPLICATE"],
            [6, "NB-06620", "NMB", "NO DATE", null, null, "abc"]);

        var dry = await ImportAsync(client, file, dryRun: true);
        Assert.Equal(3, dry.GetProperty("added").GetInt32());
        Assert.Equal(3, dry.GetProperty("errors").GetArrayLength());
        Assert.Equal(0, (await client.GetJsonAsync("/api/drawings")).GetProperty("totalCount").GetInt32());

        var done = await ImportAsync(client, file, dryRun: false);
        Assert.Equal(3, done.GetProperty("added").GetInt32());
        Assert.Equal(2, done.GetProperty("warnings").GetArrayLength()); // unreadable price, missing date

        // The old "Have a PO" tick has no number, so it is kept as a marker rather than an invented number.
        var carrier = await client.GetJsonAsync("/api/drawings/NB-06618");
        Assert.Equal("มี PO (ไม่ระบุเลขที่)", carrier.GetProperty("poNo").GetString());
        var pusher = await client.GetJsonAsync("/api/drawings/NB-06619");
        Assert.Equal("", pusher.GetProperty("poNo").GetString());
        Assert.Equal("ACRYLIC", pusher.GetProperty("material").GetString());
        Assert.Equal(1200m, pusher.GetProperty("price").GetDecimal());
        Assert.Equal("2026-08-08", pusher.GetProperty("inputDate").GetString()); // Buddhist year converted

        // Importing again skips what exists, or updates it on request.
        Assert.Equal(3, (await ImportAsync(client, file, false)).GetProperty("skipped").GetInt32());
        Assert.Equal(3, (await ImportAsync(client, file, false, "UpdateExisting")).GetProperty("updated").GetInt32());
    }

    [Fact]
    public async Task A_PO_No_column_is_imported_as_the_number()
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Sheet1");
        string[] headers = ["PdfCodeS", "PartName", "InputDate", "PO No."];
        for (var c = 0; c < headers.Length; c++) ws.Cell(1, c + 1).Value = headers[c];
        ws.Cell(2, 1).Value = "NB-06618";
        ws.Cell(2, 2).Value = "CARRIER";
        ws.Cell(2, 3).Value = new DateTime(2026, 8, 8);
        ws.Cell(2, 4).Value = "PO-7788";
        using var ms = new MemoryStream();
        wb.SaveAs(ms);

        await ImportAsync(client, ms.ToArray(), dryRun: false);
        Assert.Equal("PO-7788", (await client.GetJsonAsync("/api/drawings/NB-06618")).GetProperty("poNo").GetString());
    }

    [Fact]
    public async Task Export_can_be_imported_back()
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();
        await ImportAsync(client, Workbook([1, "NB-06618", "NMB", "CARRIER", "X", "PEEK", 3900, null, new DateTime(2026, 8, 8), "Q1", "R", "ü"]), false);

        var export = await (await client.GetAsync("/api/drawings/export")).Content.ReadAsByteArrayAsync();
        var again = await ImportAsync(client, export, dryRun: true, mode: "UpdateExisting");
        Assert.Equal(1, again.GetProperty("updated").GetInt32());
        Assert.Equal(0, again.GetProperty("errors").GetArrayLength());

        await ImportAsync(client, export, dryRun: false, mode: "UpdateExisting");
        Assert.Equal("มี PO (ไม่ระบุเลขที่)", (await client.GetJsonAsync("/api/drawings/NB-06618")).GetProperty("poNo").GetString());
    }

    [Fact]
    public async Task Imports_the_real_workbook_when_present()
    {
        var path = FindDocWorkbook();
        if (path is null)
            return; // the customer workbook is not in the repository

        var client = _app.Spa();
        await client.LoginAsAdminAsync();
        var result = await ImportAsync(client, await File.ReadAllBytesAsync(path), dryRun: false);

        Assert.Equal(["NMB-2026"], result.GetProperty("sheets").EnumerateArray().Select(s => s.GetString()));
        Assert.Equal(50, result.GetProperty("added").GetInt32());
        Assert.Equal(0, result.GetProperty("errors").GetArrayLength());
        Assert.Equal(0, result.GetProperty("warnings").GetArrayLength());
        Assert.Equal("มี PO (ไม่ระบุเลขที่)", (await client.GetJsonAsync("/api/drawings/NB-06618")).GetProperty("poNo").GetString());
        Assert.Equal("", (await client.GetJsonAsync("/api/drawings/NB-06619")).GetProperty("poNo").GetString());
    }

    private static string? FindDocWorkbook()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var doc = Path.Combine(dir.FullName, "Doc");
            if (Directory.Exists(doc))
                return Directory.EnumerateFiles(doc, "DataDrawingNMB*.xlsx").FirstOrDefault();
        }
        return null;
    }
}
