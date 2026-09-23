using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PDFHub.Application;

namespace PDFHub.Infrastructure;

public sealed class AppPaths
{
    public AppPaths(IOptions<PdfHubOptions> options, IHostEnvironment env)
    {
        DataRoot = Path.GetFullPath(Path.Combine(env.ContentRootPath, options.Value.DataRoot));
        PdfRoot = Path.Combine(DataRoot, "pdf");
        BackupRoot = Path.Combine(DataRoot, "backup");
        DatabaseFile = Path.Combine(DataRoot, "pdfhub.db");

        Directory.CreateDirectory(PdfRoot);
        Directory.CreateDirectory(BackupRoot);
    }

    public string DataRoot { get; }
    public string PdfRoot { get; }
    public string BackupRoot { get; }
    public string DatabaseFile { get; }

    public string ConnectionString => new SqliteConnectionStringBuilder { DataSource = DatabaseFile }.ToString();
}
