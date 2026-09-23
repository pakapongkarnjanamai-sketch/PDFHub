using System.Net;

namespace PDFHub.Tests;

public class DrawingCrudTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    private static KeyValuePair<string, string>[] Fields(string code, string part = "CARRIER") =>
    [
        new("Input.PdfCode", code),
        new("Input.PartName", part),
        new("Input.DrawingNo", "34281-44-0013A"),
        new("Input.Material", "PEEK"),
        new("Input.Price", "3900"),
        new("Input.InputDate", "2026-08-08"),
        new("Input.QuoNo", "Q-001"),
        new("Input.Remark", "test"),
        new("Input.HasPo", "true"),
    ];

    [Fact]
    public async Task Anonymous_can_browse_but_not_edit()
    {
        var client = _app.Browser();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/")).StatusCode);

        var edit = await client.GetAsync("/drawing/edit");
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);
        Assert.StartsWith("http://localhost/account/login", edit.Headers.Location!.ToString());

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/admin/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync("/import")).StatusCode);
    }

    [Fact]
    public async Task Default_admin_must_change_password_first()
    {
        var client = _app.Browser();
        var res = await client.PostFormAsync("/account/login", "/account/login", [new("UserName", "admin"), new("Password", "admin")]);
        Assert.Equal("/account/password", res.Headers.Location?.OriginalString);

        // Any other page sends them back to the change-password form.
        var list = await client.GetAsync("/drawing/edit");
        Assert.Equal("/account/password", list.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Create_read_update_delete_with_pdf()
    {
        var client = _app.Browser();
        await client.LoginAsAdminAsync();

        // Create — lower case and missing dash are normalized like the old macro upper-cased input.
        var res = await client.PostFormAsync("/drawing/edit", "/drawing/edit", Fields("nb06618"), ("PdfFile", "scan.pdf", Http.SamplePdf));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal("/drawing/NB-06618", res.Headers.Location?.OriginalString);

        // Read: details, list, PDF, search
        var details = await client.GetStringAsync("/drawing/NB-06618");
        Assert.Contains("CARRIER", details);
        Assert.Contains("NMB", details);
        Assert.Contains("3,900", details);

        var list = await client.GetStringAsync("/?q=peek");
        Assert.Contains("NB-06618", list);
        Assert.DoesNotContain("NB-06618", await client.GetStringAsync("/?q=nothing-matches"));

        var pdf = await client.GetAsync("/pdf/NB-06618.pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Http.SamplePdf, await pdf.Content.ReadAsByteArrayAsync());
        Assert.True(File.Exists(Path.Combine(_app.DataRoot, "pdf", "NB", "NB-06618.pdf")));

        // Update, including a code change: the stored PDF follows the new code.
        var editPage = await client.GetStringAsync("/drawing/edit/1");
        Assert.Contains("value=\"NB-06618\"", editPage);
        Assert.Contains("value=\"2026-08-08\"", editPage);

        res = await client.PostFormAsync("/drawing/edit/1", "/drawing/edit/1", Fields("NB-06700", "CARRIER REV.B"));
        Assert.Equal("/drawing/NB-06700", res.Headers.Location?.OriginalString);
        Assert.Contains("CARRIER REV.B", await client.GetStringAsync("/drawing/NB-06700"));
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/pdf/NB-06700.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/pdf/NB-06618.pdf")).StatusCode);

        // Replace the PDF: the previous file goes to _archive instead of being overwritten.
        res = await client.PostFormAsync("/drawing/edit/1", "/drawing/edit/1", Fields("NB-06700"), ("PdfFile", "rev-c.pdf", Http.SamplePdf));
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Single(Directory.GetFiles(Path.Combine(_app.DataRoot, "pdf", "_archive", "NB")));

        // Delete
        res = await client.PostFormAsync("/drawing/edit/1", "/drawing/edit/1?handler=Delete", []);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/drawing/NB-06700")).StatusCode);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_app.DataRoot, "pdf", "_archive", "NB")).Length);
    }

    [Theory]
    [InlineData("NB-066", "รูปแบบ PdfCode ไม่ถูกต้อง")]
    [InlineData("NB-06A18", "ตัวเลขท้ายต้องมี 5 หลัก")]
    [InlineData("ZZ-06618", "ไม่พบประเภท ZZ")]
    public async Task Invalid_codes_are_rejected_with_the_macro_messages(string code, string message)
    {
        var client = _app.Browser();
        await client.LoginAsAdminAsync();

        var res = await client.PostFormAsync("/drawing/edit", "/drawing/edit", Fields(code));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Contains(message, await res.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Duplicate_code_and_non_pdf_file_are_rejected()
    {
        var client = _app.Browser();
        await client.LoginAsAdminAsync();
        await client.PostFormAsync("/drawing/edit", "/drawing/edit", Fields("NB-06618"));

        var dup = await client.PostFormAsync("/drawing/edit", "/drawing/edit", Fields("NB-06618"));
        Assert.Contains("NB-06618 มีอยู่ในระบบแล้ว", await dup.Content.ReadAsStringAsync());

        var notPdf = await client.PostFormAsync("/drawing/edit", "/drawing/edit", Fields("NB-06619"), ("PdfFile", "fake.pdf", "hello"u8.ToArray()));
        Assert.Contains("ไม่ใช่ไฟล์ PDF", await notPdf.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Save_and_next_prefills_the_following_code()
    {
        var client = _app.Browser();
        await client.LoginAsAdminAsync();

        var res = await client.PostFormAsync("/drawing/edit", "/drawing/edit", [.. Fields("NB-06618"), new("then", "next")]);
        Assert.Equal("/drawing/edit?next=NB-06619&date=2026-08-08", res.Headers.Location?.OriginalString);

        var form = await client.GetStringAsync(res.Headers.Location!.OriginalString);
        Assert.Contains("value=\"NB-06619\"", form);
        Assert.Contains("value=\"2026-08-08\"", form);
    }

    [Fact]
    public async Task Export_returns_an_excel_file()
    {
        var client = _app.Browser();
        await client.LoginAsAdminAsync();
        await client.PostFormAsync("/drawing/edit", "/drawing/edit", Fields("NB-06618"));

        var res = await client.GetAsync("/?handler=Export");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", res.Content.Headers.ContentType?.MediaType);
    }
}
