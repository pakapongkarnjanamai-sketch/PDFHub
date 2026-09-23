namespace PDFHub.Domain.DTOs;

public class StartBackupDto
{
    public string? Destination { get; set; }
}

public record BackupPathCheckDto(string Destination, bool Ok, string Message);

public class BackupRunDto
{
    public int Id { get; set; }
    public string Destination { get; set; } = string.Empty;
    /// <summary>"Running" | "Succeeded" | "Failed"</summary>
    public string Status { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public string DatabaseFile { get; set; } = string.Empty;
    public int PdfTotal { get; set; }
    public int PdfCopied { get; set; }
    public int PdfSkipped { get; set; }
    public long BytesCopied { get; set; }
    public string Error { get; set; } = string.Empty;
    public string StartedBy { get; set; } = string.Empty;
}

public class BackupOverviewDto
{
    /// <summary>Pre-fills the destination box: the last destination that worked.</summary>
    public string LastDestination { get; set; } = string.Empty;
    public BackupRunDto? Running { get; set; }
    public IReadOnlyList<BackupRunDto> History { get; set; } = [];
    /// <summary>Server folder of the automatic daily database copies, for reference.</summary>
    public string DailyBackupFolder { get; set; } = string.Empty;
}
