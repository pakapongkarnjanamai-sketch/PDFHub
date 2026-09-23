using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PDFHub.Api.Extensions;
using PDFHub.Application;
using PDFHub.Application.Exceptions;
using PDFHub.Application.Services;
using PDFHub.Domain.DTOs;
using PDFHub.Domain.Entities;

namespace PDFHub.Api.Controllers;

[ApiController]
[Route("api/session")]
[AllowAnonymous]
public class SessionController(IAuthService auth, IOptions<PdfHubOptions> options) : ControllerBase
{
    [HttpGet("me")]
    public SessionDto Me()
    {
        var role = User.RoleName();
        return new SessionDto
        {
            IsAuthenticated = User.Identity?.IsAuthenticated == true,
            UserName = User.Identity?.Name ?? string.Empty,
            DisplayName = User.FindFirst(PdfHubClaims.DisplayName)?.Value ?? string.Empty,
            Role = role,
            MustChangePassword = User.MustChangePassword(),
            CanEdit = Roles.CanEdit(role),
            IsAdmin = role == Roles.Admin,
            RequireLoginToView = options.Value.RequireLoginToView,
        };
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginDto dto, CancellationToken ct)
    {
        var user = await auth.SignInAsync(dto, ct)
            ?? throw new BusinessRuleException("ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง");
        await SessionCookie.SignInAsync(HttpContext, user, dto.Remember);
        return Ok(new { success = true });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok(new { success = true });
    }

    [HttpPost("password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordDto dto, CancellationToken ct)
    {
        var user = await auth.ChangePasswordAsync(User.UserId()!.Value, dto, ct);
        // The stamp changed: re-issue this browser's cookie, other sessions of this user are signed out.
        await SessionCookie.SignInAsync(HttpContext, user);
        return Ok(new { success = true });
    }
}
