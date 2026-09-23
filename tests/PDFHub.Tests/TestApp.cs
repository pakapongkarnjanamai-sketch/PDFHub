using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace PDFHub.Tests;

/// <summary>The real app on a throw-away data folder.</summary>
public sealed class TestApp : WebApplicationFactory<Program>
{
    public string DataRoot { get; } = Path.Combine(Path.GetTempPath(), "pdfhub-tests", Guid.NewGuid().ToString("N"));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("PdfHub:DataRoot", DataRoot);
        builder.UseSetting("PdfHub:BackupKeepDays", "0");
    }

    public HttpClient Browser() => CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try { Directory.Delete(DataRoot, recursive: true); } catch (IOException) { }
    }
}

public static partial class Http
{
    public static async Task<string> TokenAsync(this HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        return TokenRegex().Match(html).Groups[1].Value;
    }

    /// <summary>Posts a form the way the browser would, including the antiforgery token from the page.</summary>
    public static async Task<HttpResponseMessage> PostFormAsync(this HttpClient client, string pageUrl, string postUrl,
        IEnumerable<KeyValuePair<string, string>> fields, (string Name, string FileName, byte[] Content)? file = null)
    {
        var token = await client.TokenAsync(pageUrl);
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        foreach (var (key, value) in fields)
            form.Add(new StringContent(value), key);
        if (file is { } f)
            form.Add(new ByteArrayContent(f.Content), f.Name, f.FileName);
        return await client.PostAsync(postUrl, form);
    }

    public static async Task LoginAsAdminAsync(this HttpClient client, string password = "admin")
    {
        var res = await client.PostFormAsync("/account/login", "/account/login",
            [new("UserName", "admin"), new("Password", password)]);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);

        // First login must change the default password.
        Assert.Equal("/account/password", res.Headers.Location?.OriginalString);
        res = await client.PostFormAsync("/account/password", "/account/password",
            [new("CurrentPassword", password), new("NewPassword", "secret123"), new("ConfirmPassword", "secret123")]);
        Assert.Equal(HttpStatusCode.Redirect, res.StatusCode);
    }

    public static readonly byte[] SamplePdf = "%PDF-1.4\n1 0 obj<<>>endobj\ntrailer<<>>\n%%EOF"u8.ToArray();

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
