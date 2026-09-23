using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PDFHub.Data;
using PDFHub.Services;

namespace PDFHub.Pages.Account;

public class LoginModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public string? UserName { get; set; }

    [BindProperty]
    public string? Password { get; set; }

    [BindProperty]
    public bool Remember { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? Error { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var name = (UserName ?? "").Trim();
        var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == name);
        if (user is null || !user.IsActive || !PasswordHasher.Verify(Password ?? "", user.PasswordHash))
        {
            Error = "ชื่อผู้ใช้หรือรหัสผ่านไม่ถูกต้อง";
            return Page();
        }

        user.LastLoginAt = DateTime.Now;
        await db.SaveChangesAsync();
        await UserSession.SignInAsync(HttpContext, user, Remember);

        if (user.MustChangePassword)
            return RedirectToPage("/Account/ChangePassword", new { returnUrl = ReturnUrl });
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "~/");
    }
}
