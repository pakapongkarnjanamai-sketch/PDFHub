using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;

namespace PDFHub;

public class PdfHubOptions
{
    public const string SectionName = "PdfHub";

    /// <summary>Folder holding the database, the PDF files and the daily backups. Relative paths are resolved against the app folder.</summary>
    public string DataRoot { get; set; } = "App_Data";

    /// <summary>When false, anyone on the network can search and open PDFs; logging in is only needed to change data.</summary>
    public bool RequireLoginToView { get; set; }

    public int MaxPdfSizeMB { get; set; } = 100;

    /// <summary>Daily database backups older than this are deleted. 0 turns the backup off.</summary>
    public int BackupKeepDays { get; set; } = 30;
}

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
