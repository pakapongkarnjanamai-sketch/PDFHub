namespace PDFHub.Domain;

/// <summary>
/// PdfCode rules carried over from the original Excel macro (Worksheet_Change) and its data-validation formula:
/// 8 characters, "XX-NNNNN", XX must be a known section code, NNNNN must be digits.
/// </summary>
public static class PdfCodeRules
{
    public const string Example = "HI-07666";

    /// <summary>Trims, upper-cases and inserts the missing dash ("nb06618" → "NB-06618").</summary>
    public static string Normalize(string? value)
    {
        var code = (value ?? string.Empty).Trim().ToUpperInvariant();
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

    /// <summary>
    /// Stored for drawings that the old sheet ticked "Have a PO" without a number. Never invent a number:
    /// this keeps the fact that a PO exists and is easy to find and replace.
    /// </summary>
    public const string PoWithoutNumber = "มี PO (ไม่ระบุเลขที่)";

    public static string RelativePdfPath(string code) => Path.Combine(code[..2], code + ".pdf");
}
