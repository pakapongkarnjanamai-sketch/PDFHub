using System.ComponentModel.DataAnnotations;

namespace PDFHub.Domain.Common;

/// <summary>Audit fields are stamped by AppDbContext.SaveChanges; services never set them.</summary>
public abstract class BaseEntity
{
    [Key]
    public int Id { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    [StringLength(100)]
    public string? CreatedBy { get; set; }
    [StringLength(100)]
    public string? UpdatedBy { get; set; }
}
