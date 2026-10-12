using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Models;

public class Invoice
{
    public int InvoiceId { get; set; }

    public int RentalContractId { get; set; }
    public RentalContract RentalContract { get; set; } = null!;

    public int? MeterReadingId { get; set; }
    public MeterReading? MeterReading { get; set; }

    [Required]
    [MaxLength(7)]
    public string BillingPeriod { get; set; } = "";

    // ===== Thành tiền =====
    [Column(TypeName = "decimal(18,2)")]
    public decimal RoomRentAmount { get; set; }

    public double ElectricUsage { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal ElectricAmount { get; set; }

    public double WaterUsage { get; set; }
    [Column(TypeName = "decimal(18,2)")]
    public decimal WaterAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal ServiceFeeAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AdjustmentAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal TotalAmount { get; set; }

    // ===== Thời gian & thanh toán =====
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime DueDate { get; set; }

    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

    [MaxLength(1000)]
    public string? VietQRContent { get; set; }

    public DateTime? TenantConfirmedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}