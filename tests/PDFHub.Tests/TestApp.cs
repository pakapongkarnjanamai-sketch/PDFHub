using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PDFHub.Tests;

/// <summary>The real API on a throw-away data folder and SQLite database.</summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), "pdfhub-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("PdfHub:DataRoot", DataRoot);
        builder.UseSetting("PdfHub:BackupKeepDays", "0");
    }

    /// <summary>A client that behaves like the SPA: keeps the cookie and sends the X-Requested-With header.</summary>
    public HttpClient Spa()
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Add("X-Requested-With", "PDFHub");
        return client;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(DataRoot, recursive: true); } catch (IOException) { }
    }
}

public static class Api
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static readonly byte[] SamplePdf = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();

    public static async Task LoginAsAdminAsync(this HttpClient client)
    {
        var res = await client.PostAsJsonAsync("/api/session/login", new { userName = "admin", password = "admin" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        res = await client.PostAsJsonAsync("/api/session/password", new { currentPassword = "admin", newPassword = "secret123" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
    }

    public static async Task<JsonElement> JsonAsync(this HttpResponseMessage res) =>
        await res.Content.ReadFromJsonAsync<JsonElement>(Json);

    public static async Task<JsonElement> GetJsonAsync(this HttpClient client, string url)
    {
        var res = await client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.JsonAsync();
    }

    public static Task<HttpResponseMessage> UploadAsync(this HttpClient client, HttpMethod method, string url,
        string fileName, byte[] content, params (string Name, string Value)[] fields)
    {
        var form = new MultipartFormDataContent { { new ByteArrayContent(content), "file", fileName } };
        foreach (var (name, value) in fields)
            form.Add(new StringContent(value), name);
        return client.SendAsync(new HttpRequestMessage(method, url) { Content = form });
    }

    public static string[] FieldErrors(this JsonElement problem, string field) =>
        problem.GetProperty("errors").TryGetProperty(field, out var list)
            ? list.EnumerateArray().Select(e => e.GetString()!).ToArray()
            : [];
}
