using PDFHub.Domain.Common;

namespace PDFHub.Domain.Entities;

/// <summary>Customer section (the "List Type" sheet of the original workbook) — the first two letters of a PdfCode.</summary>
public class Section : BaseEntity
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

/// <summary>One row of the original "NMB-2026" sheet.</summary>
public class Drawing : BaseEntity
{
    public string PdfCode { get; set; } = string.Empty;
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public string PartName { get; set; } = string.Empty;
    public string? DrawingNo { get; set; }
    public string? Material { get; set; }
    public decimal? Price { get; set; }
    public DateOnly InputDate { get; set; }
    public string? QuoNo { get; set; }
    public string? Remark { get; set; }
    public bool HasPo { get; set; }

    /// <summary>Path relative to the PDF storage root; null until a PDF is uploaded.</summary>
    public string? PdfFileName { get; set; }
    public long? PdfSize { get; set; }
    public DateTime? PdfUploadedAt { get; set; }
}

public class AppUser : BaseEntity
{
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = Roles.Viewer;
    public bool MustChangePassword { get; set; }

    /// <summary>Changes whenever credentials or permissions change, which signs out existing sessions.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime? LastLoginAt { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Editor, Viewer];

    public static bool CanEdit(string role) => role is Admin or Editor;
}

public enum BackupStatus
{
    Running,
    Succeeded,
    Failed,
}

/// <summary>One manual backup started by an admin from the Backup page.</summary>
public class BackupRun : BaseEntity
{
    public string Destination { get; set; } = string.Empty;
    public BackupStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    /// <summary>Database snapshot written by this run, relative to <see cref="Destination"/>.</summary>
    public string? DatabaseFile { get; set; }
    public int PdfTotal { get; set; }
    public int PdfCopied { get; set; }
    public int PdfSkipped { get; set; }
    public long BytesCopied { get; set; }
    public string? Error { get; set; }
}
