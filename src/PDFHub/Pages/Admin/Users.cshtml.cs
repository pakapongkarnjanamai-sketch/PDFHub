using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PDFHub.Data;
using PDFHub.Services;

namespace PDFHub.Pages.Admin;

public class UsersModel(AppDbContext db) : PageModel
{
    public const int MinPasswordLength = 6;

    public List<AppUser> Users { get; private set; } = [];

    [BindProperty]
    public NewUserInput NewUser { get; set; } = new();

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostAddAsync()
    {
        var userName = (NewUser.UserName ?? "").Trim();
        if (await db.Users.AnyAsync(u => u.UserName == userName))
            ModelState.AddModelError("NewUser.UserName", $"มีชื่อผู้ใช้ {userName} อยู่แล้ว");
        if (!Roles.All.Contains(NewUser.Role))
            ModelState.AddModelError("NewUser.Role", "สิทธิ์ไม่ถูกต้อง");

        if (!ModelState.IsValid)
        {
            await LoadAsync();
            return Page();
        }

        db.Users.Add(new AppUser
        {
            UserName = userName,
            DisplayName = string.IsNullOrWhiteSpace(NewUser.DisplayName) ? userName : NewUser.DisplayName.Trim(),
            Role = NewUser.Role,
            PasswordHash = PasswordHasher.Hash(NewUser.Password!),
            MustChangePassword = true,
            CreatedAt = DateTime.Now,
        });
        await db.SaveChangesAsync();
        this.Flash($"เพิ่มผู้ใช้ {userName} แล้ว ผู้ใช้จะต้องเปลี่ยนรหัสผ่านเมื่อเข้าสู่ระบบครั้งแรก");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdateAsync(int id, string? displayName, string role, bool active)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return NotFound();
        if (!Roles.All.Contains(role))
            return BadRequest();

        var isSelf = user.Id == User.UserId();
        if (isSelf && (role != Roles.Admin || !active))
        {
            this.Flash("ลดสิทธิ์หรือปิดบัญชีของตัวเองไม่ได้ ให้ผู้ดูแลระบบคนอื่นทำแทน", "error");
            return RedirectToPage();
        }

        if (user.Role != role || user.IsActive != active)
            user.SecurityStamp = Guid.NewGuid().ToString("N");
        user.DisplayName = string.IsNullOrWhiteSpace(displayName) ? user.UserName : displayName.Trim();
        user.Role = role;
        user.IsActive = active;
        await db.SaveChangesAsync();
        this.Flash($"บันทึกผู้ใช้ {user.UserName} แล้ว");
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(int id, string? password)
    {
        var user = await db.Users.FindAsync(id);
        if (user is null)
            return NotFound();
        if (password is null || password.Length < MinPasswordLength)
        {
            this.Flash($"รหัสผ่านชั่วคราวต้องมีอย่างน้อย {MinPasswordLength} ตัวอักษร", "error");
            return RedirectToPage();
        }

        user.PasswordHash = PasswordHasher.Hash(password);
        user.MustChangePassword = true;
        user.SecurityStamp = Guid.NewGuid().ToString("N");
        await db.SaveChangesAsync();
        this.Flash($"ตั้งรหัสผ่านชั่วคราวให้ {user.UserName} แล้ว ผู้ใช้ต้องเปลี่ยนรหัสผ่านเมื่อเข้าสู่ระบบครั้งถัดไป");
        return RedirectToPage();
    }

    private async Task LoadAsync() =>
        Users = await db.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync();

    public class NewUserInput
    {
        [Required(ErrorMessage = "กรุณากรอกชื่อผู้ใช้")]
        [RegularExpression(@"^[A-Za-z0-9._-]{2,50}$", ErrorMessage = "ใช้ได้เฉพาะ a-z, 0-9, จุด, ขีด (2–50 ตัว)")]
        public string? UserName { get; set; }

        [StringLength(100)]
        public string? DisplayName { get; set; }

        public string Role { get; set; } = Roles.Editor;

        [Required(ErrorMessage = "กรุณากรอกรหัสผ่าน")]
        [MinLength(MinPasswordLength, ErrorMessage = "รหัสผ่านต้องมีอย่างน้อย 6 ตัวอักษร")]
        public string? Password { get; set; }
    }
}
