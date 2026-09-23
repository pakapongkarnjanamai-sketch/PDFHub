namespace PDFHub.Domain.DTOs;

public class SectionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int DrawingCount { get; set; }
}

public class CreateSectionDto
{
    public string? Code { get; set; }
    public string? Name { get; set; }
}

public class UpdateSectionDto
{
    public string? Name { get; set; }
    public bool IsActive { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool MustChangePassword { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

public class CreateUserDto
{
    public string? UserName { get; set; }
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    public string? Password { get; set; }
}

public class UpdateUserDto
{
    public string? DisplayName { get; set; }
    public string? Role { get; set; }
    public bool IsActive { get; set; }
}

public class ResetPasswordDto
{
    public string? Password { get; set; }
}

public class LoginDto
{
    public string? UserName { get; set; }
    public string? Password { get; set; }
    public bool Remember { get; set; }
}

public class ChangePasswordDto
{
    public string? CurrentPassword { get; set; }
    public string? NewPassword { get; set; }
}

/// <summary>
/// GET /api/session/me. The Can* flags are display state for the client — every endpoint still
/// enforces its own policy.
/// </summary>
public class SessionDto
{
    public bool IsAuthenticated { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public bool MustChangePassword { get; set; }
    public bool CanEdit { get; set; }
    public bool IsAdmin { get; set; }
    /// <summary>When false, anonymous visitors may browse drawings and open PDFs.</summary>
    public bool RequireLoginToView { get; set; }
}

public enum ImportMode
{
    /// <summary>Only add codes that are not in the database yet.</summary>
    SkipExisting,
    /// <summary>Overwrite existing drawings with the values in the file.</summary>
    UpdateExisting,
}

public record ImportIssueDto(string Sheet, int Row, string? Code, string Message);

public class ImportResultDto
{
    public bool DryRun { get; set; }
    public List<string> Sheets { get; } = [];
    public int RowsRead { get; set; }
    public int Added { get; set; }
    public int Updated { get; set; }
    public int Skipped { get; set; }
    public List<ImportIssueDto> Errors { get; } = [];
    public List<ImportIssueDto> Warnings { get; } = [];
}
