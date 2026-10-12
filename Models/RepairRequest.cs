using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Models;

public class RepairRequest
{
    public int RepairRequestId { get; set; }

    public int RentalPostId { get; set; }
    public RentalPost RentalPost { get; set; } = null!;

    public string TenantId { get; set; } = "";
    public ApplicationUser Tenant { get; set; } = null!;

    [Required]
    [MaxLength(100)]
    public string ProblemType { get; set; } = "";

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = "";

    public RepairPriority Priority { get; set; } = RepairPriority.Medium;

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    public RepairRequestStatus Status { get; set; } = RepairRequestStatus.Pending;

    [MaxLength(2000)]
    public string? ResolutionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}