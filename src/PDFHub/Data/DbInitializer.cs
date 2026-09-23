using Microsoft.EntityFrameworkCore;
using PDFHub.Services;

namespace PDFHub.Data;

public static class DbInitializer
{
    // ชีต "List Type" ในไฟล์ Excel เดิม
    private static readonly (string Code, string Name)[] DefaultSections =
    [
        ("BL", "BALL BPI"), ("BW", "BANWA"), ("CT", "CENTER"), ("GM", "GA-MA"), ("HI", "HI-TECH"),
        ("MC", "MCT"), ("NT", "NHT"), ("NB", "NMB"), ("PM", "PELMEC"), ("PD", "PTD"),
        ("SP", "SPD-2"), ("DC", "DIE CAST"), ("RB", "RUBBER SEAL"),
    ];

    public const string DefaultAdminUser = "admin";
    public const string DefaultAdminPassword = "admin";

    public static async Task InitializeAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
        // WAL lets people keep reading while someone else is saving.
        await db.Database.ExecuteSqlRawAsync("PRAGMA journal_mode=WAL;");

        if (!await db.Sections.AnyAsync())
            db.Sections.AddRange(DefaultSections.Select(s => new Section { Code = s.Code, Name = s.Name }));

        if (!await db.Users.AnyAsync())
        {
            db.Users.Add(new AppUser
            {
                UserName = DefaultAdminUser,
                DisplayName = "ผู้ดูแลระบบ",
                Role = Roles.Admin,
                PasswordHash = PasswordHasher.Hash(DefaultAdminPassword),
                MustChangePassword = true,
                CreatedAt = DateTime.Now,
            });
        }

        await db.SaveChangesAsync();
    }
}
