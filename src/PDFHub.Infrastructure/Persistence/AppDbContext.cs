using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PDFHub.Application.Abstractions;
using PDFHub.Domain.Common;
using PDFHub.Domain.Entities;

namespace PDFHub.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options, IDateTime dateTime, ICurrentUserService currentUser)
    : DbContext(options)
{
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Drawing> Drawings => Set<Drawing>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Section>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(2).IsRequired();
            e.HasIndex(x => x.Code).IsUnique().HasDatabaseName("IX_Sections_Code");
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        b.Entity<Drawing>(e =>
        {
            e.Property(x => x.PdfCode).HasMaxLength(8).IsRequired();
            e.HasIndex(x => x.PdfCode).IsUnique().HasDatabaseName("IX_Drawings_PdfCode");
            e.Property(x => x.PartName).HasMaxLength(200).IsRequired();
            e.Property(x => x.DrawingNo).HasMaxLength(100);
            e.Property(x => x.Material).HasMaxLength(100);
            // SQLite has no decimal type, and EF cannot ORDER BY a decimal stored as TEXT.
            e.Property(x => x.Price).HasConversion<double?>();
            e.Property(x => x.QuoNo).HasMaxLength(50);
            e.Property(x => x.Remark).HasMaxLength(1000);
            e.Property(x => x.PdfFileName).HasMaxLength(260);
            e.HasIndex(x => x.InputDate);
            e.HasOne(x => x.Section).WithMany().HasForeignKey(x => x.SectionId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<AppUser>(e =>
        {
            e.Property(x => x.UserName).HasMaxLength(50).IsRequired().UseCollation("NOCASE");
            e.HasIndex(x => x.UserName).IsUnique().HasDatabaseName("IX_Users_UserName");
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.Role).HasMaxLength(20);
            e.Property(x => x.PasswordHash).HasMaxLength(200);
            e.Property(x => x.SecurityStamp).HasMaxLength(64);
        });
    }

    public override int SaveChanges()
    {
        SetAuditFields();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SetAuditFields();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void SetAuditFields()
    {
        var user = string.IsNullOrEmpty(currentUser.UserId) ? null : currentUser.UserId;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = dateTime.Now;
                entry.Entity.CreatedBy = user;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = dateTime.Now;
                entry.Entity.UpdatedBy = user;

                // Update(entity) marks every property modified, which would reset these to their defaults.
                entry.Property(x => x.CreatedAt).IsModified = false;
                entry.Property(x => x.CreatedBy).IsModified = false;
            }
        }
    }
}

/// <summary>Used only by `dotnet ef migrations`.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=design.db").Options,
        new Services.DateTimeService(),
        new DesignTimeUser());

    private sealed class DesignTimeUser : ICurrentUserService
    {
        public string UserId => "";
        public string FullName => "";
        public bool IsAuthenticated => false;
    }
}
