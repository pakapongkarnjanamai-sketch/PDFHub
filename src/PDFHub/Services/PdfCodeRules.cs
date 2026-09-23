using Microsoft.EntityFrameworkCore;
using PDFHub.Data;

namespace PDFHub.Services;

/// <summary>
/// PdfCode rules carried over from the original Excel macro (Worksheet_Change) and its data-validation formula:
/// 8 characters, "XX-NNNNN", XX must be a known section code, NNNNN must be digits.
/// </summary>
public static class PdfCodeRules
{
    public const string Example = "HI-07666";
    public const string StickerHint = "กรุณากรอกเฉพาะรหัสที่มีใน Sticker Minebear เท่านั้น เช่น BL-xxxxx";

    /// <summary>Trims, upper-cases and inserts the missing dash ("nb06618" → "NB-06618").</summary>
    public static string Normalize(string? value)
    {
        var code = (value ?? "").Trim().ToUpperInvariant();
        if (code.Length == 7 && char.IsAsciiLetter(code[0]) && char.IsAsciiLetter(code[1]) && code[2..].All(char.IsAsciiDigit))
            code = $"{code[..2]}-{code[2..]}";
        return code;
    }

    /// <summary>Returns the error message for a badly formed (normalized) code, or null when the format is fine.</summary>
    public static string? FormatError(string code)
    {
        if (code.Length == 0)
            return "กรุณากรอก PdfCode";
        if (code.Length != 8 || code[2] != '-' || !char.IsAsciiLetter(code[0]) || !char.IsAsciiLetter(code[1]))
            return $"รูปแบบ PdfCode ไม่ถูกต้อง ตัวอย่าง : {Example}";
        if (!code[3..].All(char.IsAsciiDigit))
            return "ตัวเลขท้ายต้องมี 5 หลัก";
        return null;
    }

    public static string UnknownSection(string prefix) => $"ไม่พบประเภท {prefix} ในรายการ Section";

    /// <summary>"NB-06618" → "NB-06619"; null when the code is invalid or already at 99999.</summary>
    public static string? Next(string code)
    {
        if (FormatError(code) is not null)
            return null;
        var number = int.Parse(code[3..]) + 1;
        return number > 99999 ? null : $"{code[..3]}{number:D5}";
    }
}

public sealed record PdfCodeCheck(string Code, string? Error, Section? Section)
{
    public bool IsValid => Error is null;
}

public sealed class PdfCodeValidator(AppDbContext db)
{
    /// <param name="drawingId">The drawing being edited, so it does not count as its own duplicate.</param>
    public async Task<PdfCodeCheck> CheckAsync(string? raw, int? drawingId, CancellationToken ct = default)
    {
        var code = PdfCodeRules.Normalize(raw);
        if (PdfCodeRules.FormatError(code) is { } formatError)
            return new(code, formatError, null);

        var prefix = code[..2];
        var section = await db.Sections.AsNoTracking().FirstOrDefaultAsync(s => s.Code == prefix, ct);
        if (section is null)
            return new(code, PdfCodeRules.UnknownSection(prefix), null);

        var current = drawingId is null
            ? null
            : await db.Drawings.AsNoTracking().Where(d => d.Id == drawingId).Select(d => d.PdfCode).FirstOrDefaultAsync(ct);

        // A drawing may keep its code after its section is retired, but no new codes go into a retired section.
        if (!section.IsActive && current != code)
            return new(code, $"Section {prefix} ถูกปิดใช้งานแล้ว", section);

        if (current != code && await db.Drawings.AnyAsync(d => d.PdfCode == code, ct))
            return new(code, $"PdfCode {code} มีอยู่ในระบบแล้ว", section);

        return new(code, null, section);
    }
}
