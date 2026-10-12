using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EZ_Rent.Models;

/// <summary>
/// Phí dịch vụ cố định gắn với từng phòng (vệ sinh, internet, gửi xe, ...).
/// </summary>
public class Service
{
    public int ServiceId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = "";

    [Column(TypeName = "decimal(18,2)")]
    public decimal UnitPrice { get; set; }

    [MaxLength(30)]
    public string Unit { get; set; } = "tháng";

    [MaxLength(500)]
    public string? Description { get; set; }

    public int RentalPostId { get; set; }
    public RentalPost RentalPost { get; set; } = null!;
}