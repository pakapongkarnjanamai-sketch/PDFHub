using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using PDFHub.Application;
using PDFHub.Application.Services;
using PDFHub.Domain.Entities;

namespace PDFHub.Api.Extensions;

public static class SessionCookie
{
    private const string StampClaim = "pdfhub:stamp";
    public const string MustChangePasswordClaim = "pdfhub:must-change-password";

    public static Task SignInAsync(HttpContext http, SessionUser user, bool remember = false)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.Role, user.Role),
            new(PdfHubClaims.DisplayName, user.DisplayName),
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
        var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
        if (principal?.UserId() is not { } id || !await auth.IsSessionValidAsync(id, principal.FindFirstValue(StampClaim)))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }

    public static int? UserId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static bool MustChangePassword(this ClaimsPrincipal user) => user.HasClaim(MustChangePasswordClaim, "1");

    public static string RoleName(this ClaimsPrincipal user) =>
        Roles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;
}

public static class Policies
{
    public const string CanEdit = "CanEdit";
    public const string Admin = "Admin";
}
