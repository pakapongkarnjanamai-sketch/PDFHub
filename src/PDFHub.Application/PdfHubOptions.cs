namespace PDFHub.Application;

/// <summary>The "PdfHub" section of appsettings.json; bound and validated in Infrastructure.</summary>
public class PdfHubOptions
{
    public const string SectionName = "PdfHub";

    /// <summary>
    /// Folder holding the database, the PDF files and the daily backups. Relative paths resolve against
    /// the app folder. In production point it outside the IIS folder (e.g. D:\PDFHubData) so a deploy
    /// never touches data, and give the app pool identity Modify rights on it.
    /// </summary>
    public string DataRoot { get; set; } = "App_Data";

    /// <summary>When false, anyone on the network can search and open PDFs; logging in is only needed to change data.</summary>
    public bool RequireLoginToView { get; set; }

    public int MaxPdfSizeMB { get; set; } = 100;

    /// <summary>Daily database backups older than this are deleted. 0 turns the backup off.</summary>
    public int BackupKeepDays { get; set; } = 30;
}

public static class PdfHubClaims
{
    public const string DisplayName = "pdfhub:display";
}
