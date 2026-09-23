using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using PDFHub.Data;

namespace PDFHub.Services;

public static class UserSession
{
    private const string StampClaim = "pdfhub:stamp";
    private const string DisplayNameClaim = "pdfhub:display";
    public const string MustChangePasswordClaim = "pdfhub:must-change-password";

    public static Task SignInAsync(HttpContext http, AppUser user, bool remember = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role),
            new(DisplayNameClaim, user.DisplayName),
            new(StampClaim, user.SecurityStamp),
        };
        if (user.MustChangePassword)
            claims.Add(new(MustChangePasswordClaim, "1"));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        var properties = new AuthenticationProperties { IsPersistent = remember };
        if (remember)
            properties.ExpiresUtc = DateTimeOffset.UtcNow.AddDays(30);
        return http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, properties);
    }

    /// <summary>Signs a user out on their next request once an admin disables them or changes their role or password.</summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal is null || !int.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        {
            context.RejectPrincipal();
            return;
        }

        var db = context.HttpContext.RequestServices.GetRequiredService<AppDbContext>();
        var stamp = await db.Users.Where(u => u.Id == id && u.IsActive).Select(u => u.SecurityStamp).FirstOrDefaultAsync();
        if (stamp is null || stamp != principal.FindFirstValue(StampClaim))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    public static int? UserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static string DisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(DisplayNameClaim) is { Length: > 0 } name ? name : user.Identity?.Name ?? "";

    public static bool CanEdit(this ClaimsPrincipal user) => user.IsInRole(Roles.Admin) || user.IsInRole(Roles.Editor);
}
