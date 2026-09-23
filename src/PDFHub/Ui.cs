using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace PDFHub;

/// <summary>Small formatting helpers shared by the Razor pages.</summary>
public static class Ui
{
    public static HtmlString Icon(string name, string? cssClass = null) =>
        new($"<svg class=\"icon {cssClass}\" aria-hidden=\"true\"><use href=\"#i-{name}\"/></svg>");

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string DateTime(DateTime? value) =>
        value?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "—";

    public static string Price(decimal? price) =>
        price?.ToString("#,##0.##", CultureInfo.InvariantCulture) ?? "";

    public static string FileSize(long? bytes) => bytes switch
    {
        null => "",
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0} KB",
        _ => $"{bytes / (1024.0 * 1024):0.0} MB",
    };

    public static void Flash(this PageModel page, string message, string kind = "success")
    {
        page.TempData["Flash"] = message;
        page.TempData["FlashKind"] = kind;
    }
}
