using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Models;

/// <summary>
/// Yêu cầu thuê ban đầu – tiền đề để Landlord tạo hợp đồng.
/// </summary>
public class RentalRequest
{
    public int RentalRequestId { get; set; }

    [MaxLength(1000)]
    public string? Message { get; set; }

    public RentalRequestStatus Status { get; set; } = RentalRequestStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public string TenantId { get; set; } = "";
    public ApplicationUser Tenant { get; set; } = null!;

    public int RentalPostId { get; set; }
    public RentalPost RentalPost { get; set; } = null!;
}