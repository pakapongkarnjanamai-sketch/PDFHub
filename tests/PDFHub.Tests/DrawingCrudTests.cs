using System.Net;
using System.Net.Http.Json;

namespace PDFHub.Tests;

public class DrawingCrudTests : IDisposable
{
    private readonly TestApp _app = new();

    public void Dispose() => _app.Dispose();

    private static object Drawing(string code, string part = "CARRIER") => new
    {
        pdfCode = code,
        partName = part,
        drawingNo = "34281-44-0013A",
        material = "PEEK",
        price = 3900,
        inputDate = "2026-08-08",
        quoNo = "Q-001",
        remark = "test",
        poNo = "PO-2026-0815",
    };

    [Fact]
    public async Task Anonymous_can_browse_but_not_write()
    {
        var client = _app.Spa();

        var me = await client.GetJsonAsync("/api/session/me");
        Assert.False(me.GetProperty("isAuthenticated").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/drawings")).StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/drawings", Drawing("NB-06618"))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/no-such-thing")).StatusCode);
    }

    [Fact]
    public async Task Writes_without_the_spa_header_are_refused()
    {
        var client = _app.CreateClient();
        var res = await client.PostAsJsonAsync("/api/session/login", new { userName = "admin", password = "admin" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Default_admin_must_change_password_first()
    {
        var client = _app.Spa();
        var res = await client.PostAsJsonAsync("/api/session/login", new { userName = "admin", password = "admin" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await client.GetJsonAsync("/api/session/me")).GetProperty("mustChangePassword").GetBoolean());

        var blocked = await client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, blocked.StatusCode);
        Assert.Equal("password-change-required", (await blocked.JsonAsync()).GetProperty("type").GetString());

        res = await client.PostAsJsonAsync("/api/session/password", new { currentPassword = "admin", newPassword = "secret123" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var me = await client.GetJsonAsync("/api/session/me");
        Assert.False(me.GetProperty("mustChangePassword").GetBoolean());
        Assert.True(me.GetProperty("isAdmin").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/users")).StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_rejected()
    {
        var res = await _app.Spa().PostAsJsonAsync("/api/session/login", new { userName = "admin", password = "nope" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal("ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง", (await res.JsonAsync()).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task Create_read_update_delete_with_pdf()
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();

        // Create — lower case and missing dash are normalized like the old macro upper-cased input.
        var res = await client.PostAsJsonAsync("/api/drawings", Drawing("nb06618"));
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);
        var created = await res.JsonAsync();
        var id = created.GetProperty("id").GetInt32();
        Assert.Equal("NB-06618", created.GetProperty("pdfCode").GetString());
        Assert.Equal("NMB", created.GetProperty("sectionName").GetString());
        Assert.Equal("NB-06619", created.GetProperty("nextCode").GetString());
        Assert.Equal("admin", created.GetProperty("createdBy").GetString());

        res = await client.UploadAsync(HttpMethod.Put, $"/api/drawings/{id}/pdf", "scan.pdf", Api.SamplePdf);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.True((await res.JsonAsync()).GetProperty("hasPdf").GetBoolean());
        Assert.True(File.Exists(Path.Combine(_app.DataRoot, "pdf", "NB", "NB-06618.pdf")));

        // Read: detail, search, PDF
        var detail = await client.GetJsonAsync("/api/drawings/nb-06618");
        Assert.Equal(3900m, detail.GetProperty("price").GetDecimal());
        Assert.Equal("2026-08-08", detail.GetProperty("inputDate").GetString());
        Assert.Equal("PO-2026-0815", detail.GetProperty("poNo").GetString());

        var list = await client.GetJsonAsync("/api/drawings?q=peek%20carrier");
        Assert.Equal(1, list.GetProperty("filteredCount").GetInt32());
        Assert.Equal(0, (await client.GetJsonAsync("/api/drawings?q=nothing-matches")).GetProperty("filteredCount").GetInt32());
        Assert.Equal(1, (await client.GetJsonAsync("/api/drawings?q=PO-2026")).GetProperty("filteredCount").GetInt32());
        Assert.Equal(0, (await client.GetJsonAsync("/api/drawings?po=no")).GetProperty("filteredCount").GetInt32());
        Assert.Equal(1, (await client.GetJsonAsync("/api/drawings?po=yes&pdf=has&year=2026&section=NB")).GetProperty("filteredCount").GetInt32());
        // Filter options come from the unfiltered set, so they survive an active filter.
        Assert.Equal(13, (await client.GetJsonAsync("/api/drawings?section=XX")).GetProperty("filterOptions").GetProperty("sections").GetArrayLength());

        var pdf = await client.GetAsync("/pdf/NB-06618.pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        Assert.Equal(Api.SamplePdf, await pdf.Content.ReadAsByteArrayAsync());

        // Update with a code change: the stored PDF follows the new code.
        res = await client.PutAsJsonAsync($"/api/drawings/{id}", Drawing("NB-06700", "CARRIER REV.B"));
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var updated = await res.JsonAsync();
        Assert.Equal("CARRIER REV.B", updated.GetProperty("partName").GetString());
        Assert.Equal("admin", updated.GetProperty("updatedBy").GetString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/pdf/NB-06700.pdf")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/pdf/NB-06618.pdf")).StatusCode);

        // Replace the PDF: the previous file goes to _archive instead of being overwritten.
        res = await client.UploadAsync(HttpMethod.Put, $"/api/drawings/{id}/pdf", "rev-c.pdf", Api.SamplePdf);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Single(Directory.GetFiles(Path.Combine(_app.DataRoot, "pdf", "_archive", "NB")));

        // Delete
        Assert.Equal(HttpStatusCode.OK, (await client.DeleteAsync($"/api/drawings/{id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/drawings/NB-06700")).StatusCode);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_app.DataRoot, "pdf", "_archive", "NB")).Length);
    }

    [Theory]
    [InlineData("NB-066", "รูปแบบ PdfCode ไม่ถูกต้อง ตัวอย่าง : HI-07666")]
    [InlineData("NB-06A18", "ตัวเลขท้ายต้องมี 5 หลัก")]
    [InlineData("ZZ-06618", "ไม่พบประเภท ZZ ในรายการ Section")]
    public async Task Invalid_codes_return_the_macro_messages_as_field_errors(string code, string message)
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();

        var res = await client.PostAsJsonAsync("/api/drawings", Drawing(code));
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.Equal([message], (await res.JsonAsync()).FieldErrors("PdfCode"));

        var check = await client.GetJsonAsync($"/api/drawings/check-code?code={code}");
        Assert.False(check.GetProperty("valid").GetBoolean());
        Assert.Equal(message, check.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Duplicate_code_missing_fields_and_fake_pdf_are_rejected()
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();
        var created = await (await client.PostAsJsonAsync("/api/drawings", Drawing("NB-06618"))).JsonAsync();

        var dup = await (await client.PostAsJsonAsync("/api/drawings", Drawing("NB-06618"))).JsonAsync();
        Assert.Equal(["PdfCode NB-06618 มีอยู่ในระบบแล้ว"], dup.FieldErrors("PdfCode"));

        var empty = await (await client.PostAsJsonAsync("/api/drawings", new { pdfCode = "NB-06619" })).JsonAsync();
        Assert.NotEmpty(empty.FieldErrors("PartName"));
        Assert.NotEmpty(empty.FieldErrors("InputDate"));

        var fake = await client.UploadAsync(HttpMethod.Put, $"/api/drawings/{created.GetProperty("id").GetInt32()}/pdf", "fake.pdf", "hello"u8.ToArray());
        Assert.Equal(["ไฟล์นี้ไม่ใช่ PDF"], (await fake.JsonAsync()).FieldErrors("File"));
    }

    [Fact]
    public async Task Bulk_upload_matches_files_by_name()
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();
        await client.PostAsJsonAsync("/api/drawings", Drawing("NB-06618"));

        async Task<string> Upload(string name, bool overwrite = false)
        {
            var res = await client.UploadAsync(HttpMethod.Post, "/api/drawings/pdf-bulk", name, Api.SamplePdf, ("overwrite", overwrite.ToString()));
            return (await res.JsonAsync()).GetProperty("status").GetString()!;
        }

        Assert.Equal("ok", await Upload("NB-06618.pdf"));
        Assert.Equal("skip", await Upload("NB-06618.pdf"));
        Assert.Equal("ok", await Upload("nb06618.PDF", overwrite: true));
        Assert.Equal("error", await Upload("NB-09999.pdf"));
        Assert.Equal("error", await Upload("scan001.pdf"));
    }

    [Fact]
    public async Task Export_returns_the_filtered_rows_as_excel()
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();
        await client.PostAsJsonAsync("/api/drawings", Drawing("NB-06618"));

        var res = await client.GetAsync("/api/drawings/export?q=carrier");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", res.Content.Headers.ContentType?.MediaType);

        using var workbook = new ClosedXML.Excel.XLWorkbook(await res.Content.ReadAsStreamAsync());
        Assert.Equal("NB-06618", workbook.Worksheet(1).Cell(3, 2).GetString());
    }

    [Fact]
    public async Task Admin_cannot_lock_themselves_out()
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();
        var admin = (await client.GetJsonAsync("/api/users"))[0];

        var res = await client.PutAsJsonAsync($"/api/users/{admin.GetProperty("id").GetInt32()}",
            new { displayName = "x", role = "Editor", isActive = true });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }
}
