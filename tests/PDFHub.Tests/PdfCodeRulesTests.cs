using PDFHub.Domain;

namespace PDFHub.Tests;

public class PdfCodeRulesTests
{
    [Theory]
    [InlineData(" nb-06618 ", "NB-06618")]
    [InlineData("nb06618", "NB-06618")]
    [InlineData("HI-07666", "HI-07666")]
    [InlineData(null, "")]
    public void Normalize_trims_uppercases_and_adds_the_dash(string? input, string expected) =>
        Assert.Equal(expected, PdfCodeRules.Normalize(input));

    [Theory]
    [InlineData("NB-06618", null)]
    [InlineData("", "กรุณากรอก PdfCode")]
    [InlineData("NB-0661", "รูปแบบ PdfCode ไม่ถูกต้อง ตัวอย่าง : HI-07666")]
    [InlineData("NB_06618", "รูปแบบ PdfCode ไม่ถูกต้อง ตัวอย่าง : HI-07666")]
    [InlineData("12-06618", "รูปแบบ PdfCode ไม่ถูกต้อง ตัวอย่าง : HI-07666")]
    [InlineData("NB-0661X", "ตัวเลขท้ายต้องมี 5 หลัก")]
    public void FormatError_matches_the_excel_macro(string code, string? expected) =>
        Assert.Equal(expected, PdfCodeRules.FormatError(code));

    [Theory]
    [InlineData("NB-06618", "NB-06619")]
    [InlineData("NB-00099", "NB-00100")]
    [InlineData("NB-99999", null)]
    [InlineData("bad", null)]
    public void Next_is_the_following_sticker_code(string code, string? expected) =>
        Assert.Equal(expected, PdfCodeRules.Next(code));
}
