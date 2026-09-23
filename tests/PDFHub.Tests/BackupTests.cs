using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace PDFHub.Tests;

public class BackupTests : IDisposable
{
    private readonly TestApp _app = new();
    private readonly string _destination = Path.Combine(Path.GetTempPath(), "pdfhub-backup-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        _app.Dispose();
        try { Directory.Delete(_destination, recursive: true); } catch (IOException) { }
    }

    private async Task<HttpClient> AdminWithOnePdfAsync()
    {
        var client = _app.Spa();
        await client.LoginAsAdminAsync();
        var created = await (await client.PostAsJsonAsync("/api/drawings",
            new { pdfCode = "NB-06618", partName = "CARRIER", inputDate = "2026-08-08" })).JsonAsync();
        await client.UploadAsync(HttpMethod.Put, $"/api/drawings/{created.GetProperty("id").GetInt32()}/pdf", "a.pdf", Api.SamplePdf);
        return client;
    }

    /// <summary>The copy runs in the background: poll the overview until the run leaves "Running".</summary>
    private static async Task<JsonElement> WaitForRunAsync(HttpClient client, int id)
    {
        for (var i = 0; i < 100; i++)
        {
            var run = (await client.GetJsonAsync("/api/backups")).GetProperty("history").EnumerateArray()
                .First(r => r.GetProperty("id").GetInt32() == id);
            if (run.GetProperty("status").GetString() != "Running")
                return run;
            await Task.Delay(100);
        }
        throw new TimeoutException("Backup did not finish.");
    }

    private static async Task<JsonElement> StartAsync(HttpClient client, string destination)
    {
        var res = await client.PostAsJsonAsync("/api/backups", new { destination });
        Assert.Equal(HttpStatusCode.Accepted, res.StatusCode);
        return await WaitForRunAsync(client, (await res.JsonAsync()).GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Backup_copies_a_database_snapshot_and_the_pdfs()
    {
        var client = await AdminWithOnePdfAsync();

        var run = await StartAsync(client, _destination);
        Assert.Equal("Succeeded", run.GetProperty("status").GetString());
        Assert.Equal(1, run.GetProperty("pdfCopied").GetInt32());
        Assert.Equal("admin", run.GetProperty("startedBy").GetString());
        Assert.True(File.Exists(Path.Combine(_destination, run.GetProperty("databaseFile").GetString()!)));
        Assert.Equal(Api.SamplePdf, File.ReadAllBytes(Path.Combine(_destination, "PDFHub", "pdf", "NB", "NB-06618.pdf")));

        // The next run only copies what changed, and remembers the destination.
        await Task.Delay(1100); // new snapshot name (seconds resolution)
        var second = await StartAsync(client, _destination);
        Assert.Equal(0, second.GetProperty("pdfCopied").GetInt32());
        Assert.Equal(1, second.GetProperty("pdfSkipped").GetInt32());
        Assert.Equal(2, Directory.GetFiles(Path.Combine(_destination, "PDFHub", "db")).Length);
        Assert.Equal(_destination, (await client.GetJsonAsync("/api/backups")).GetProperty("lastDestination").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("relative\\folder")]
    public async Task Invalid_destinations_are_rejected(string destination)
    {
        var client = await AdminWithOnePdfAsync();

        var check = await (await client.PostAsJsonAsync("/api/backups/check", new { destination })).JsonAsync();
        Assert.False(check.GetProperty("ok").GetBoolean());

        var res = await client.PostAsJsonAsync("/api/backups", new { destination });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
        Assert.NotEmpty((await res.JsonAsync()).FieldErrors("Destination"));
    }

    [Fact]
    public async Task The_data_folder_itself_is_rejected()
    {
        var client = await AdminWithOnePdfAsync();
        var check = await (await client.PostAsJsonAsync("/api/backups/check", new { destination = _app.DataRoot })).JsonAsync();
        Assert.False(check.GetProperty("ok").GetBoolean());
    }

    [Fact]
    public async Task Only_admins_can_back_up()
    {
        var client = _app.Spa();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/backups")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.PostAsJsonAsync("/api/backups", new { destination = _destination })).StatusCode);
    }
}
