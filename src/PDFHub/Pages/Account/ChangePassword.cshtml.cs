using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PDFHub.Data;
using PDFHub.Pages.Admin;
using PDFHub.Services;

namespace PDFHub.Pages.Account;

[Authorize]
public class ChangePasswordModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public string? CurrentPassword { get; set; }

    [BindProperty]
    public string? NewPassword { get; set; }

    [BindProperty]
    public string? ConfirmPassword { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public bool Forced => User.HasClaim(UserSession.MustChangePasswordClaim, "1");

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await db.Users.FindAsync(User.UserId());
        if (user is null)
            return Challenge();

        if (!PasswordHasher.Verify(CurrentPassword ?? "", user.PasswordHash))
            ModelState.AddModelError(nameof(CurrentPassword), "รหัสผ่านปัจจุบันไม่ถูกต้อง");
        if ((NewPassword ?? "").Length < UsersModel.MinPasswordLength)
            ModelState.AddModelError(nameof(NewPassword), $"รหัสผ่านใหม่ต้องมีอย่างน้อย {UsersModel.MinPasswordLength} ตัวอักษร");
        else if (NewPassword == CurrentPassword)
            ModelState.AddModelError(nameof(NewPassword), "รหัสผ่านใหม่ต้องไม่ซ้ำกับรหัสเดิม");
        if (NewPassword != ConfirmPassword)
            ModelState.AddModelError(nameof(ConfirmPassword), "ยืนยันรหัสผ่านไม่ตรงกัน");

        if (!ModelState.IsValid)
            return Page();

        user.PasswordHash = PasswordHasher.Hash(NewPassword!);
        user.MustChangePassword = false;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();

        // The stamp changed, so re-issue this browser's cookie; other sessions of this user are signed out.
        await UserSession.SignInAsync(HttpContext, user);
        this.Flash("เปลี่ยนรหัสผ่านเรียบร้อยแล้ว");
        return LocalRedirect(Url.IsLocalUrl(ReturnUrl) ? ReturnUrl : "~/");
    }
}
