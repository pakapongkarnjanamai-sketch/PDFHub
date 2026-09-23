namespace PDFHub.Data;

/// <summary>ประเภท/แผนกลูกค้า (ชีต "List Type" เดิม) — 2 ตัวอักษรแรกของ PdfCode</summary>
public class Section
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

/// <summary>1 แถวในชีต NMB-2026 เดิม</summary>
public class Drawing
{
    public int Id { get; set; }
    public string PdfCode { get; set; } = "";
    public int SectionId { get; set; }
    public Section Section { get; set; } = null!;
    public string PartName { get; set; } = "";
    public string? DrawingNo { get; set; }
    public string? Material { get; set; }
    public decimal? Price { get; set; }
    public DateOnly InputDate { get; set; }
    public string? QuoNo { get; set; }
    public string? Remark { get; set; }
    public bool HasPo { get; set; }

    /// <summary>Path relative to the PDF storage root, null when no PDF has been uploaded yet.</summary>
    public string? PdfFileName { get; set; }
    public long? PdfSize { get; set; }
    public DateTime? PdfUploadedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}

public class AppUser
{
    public int Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = Roles.Viewer;
    public bool IsActive { get; set; } = true;
    public bool MustChangePassword { get; set; }

    /// <summary>Changes whenever credentials or permissions change, which signs out existing sessions.</summary>
    public string SecurityStamp { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public static class Roles
{
    public const string Admin = "Admin";
    public const string Editor = "Editor";
    public const string Viewer = "Viewer";

    public static readonly string[] All = [Admin, Editor, Viewer];

    public static string Label(string role) => role switch
    {
        Admin => "ผู้ดูแลระบบ",
        Editor => "ผู้บันทึกข้อมูล",
        _ => "ผู้ดูข้อมูล",
    };
}

public static class Policies
{
    public const string CanEdit = "CanEdit";
    public const string Admin = "Admin";
}
