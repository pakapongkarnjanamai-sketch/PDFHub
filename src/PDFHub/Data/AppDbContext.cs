using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PDFHub.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<Drawing> Drawings => Set<Drawing>();
    public DbSet<AppUser> Users => Set<AppUser>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Section>(e =>
        {
            e.Property(x => x.Code).HasMaxLength(2).IsRequired();
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
        });

        b.Entity<Drawing>(e =>
        {
            e.Property(x => x.PdfCode).HasMaxLength(8).IsRequired();
            e.HasIndex(x => x.PdfCode).IsUnique();
            e.Property(x => x.PartName).HasMaxLength(200).IsRequired();
            e.Property(x => x.DrawingNo).HasMaxLength(100);
            e.Property(x => x.Material).HasMaxLength(100);
            // SQLite has no decimal type and EF cannot ORDER BY a decimal stored as TEXT.
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
            e.HasIndex(x => x.UserName).IsUnique();
            e.Property(x => x.DisplayName).HasMaxLength(100);
            e.Property(x => x.Role).HasMaxLength(20);
        });
    }
}

/// <summary>Used only by `dotnet ef migrations`.</summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite("Data Source=design.db").Options);
}
