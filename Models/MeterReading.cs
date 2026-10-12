using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Models;

public class MeterReading
{
    public int MeterReadingId { get; set; }

    public int RentalPostId { get; set; }
    public RentalPost RentalPost { get; set; } = null!;

    [Required]
    [MaxLength(7)]
    public string Period { get; set; } = "";

    public DateTime ReadingDate { get; set; } = DateTime.UtcNow;

    // ===== Điện =====
    public double PrevElectricIndex { get; set; }
    public double CurrElectricIndex { get; set; }
    public double ElectricUsage { get; set; }

    [MaxLength(500)]
    public string? ElectricMeterImageUrl { get; set; }

    // ===== Nước =====
    public double PrevWaterIndex { get; set; }
    public double CurrWaterIndex { get; set; }
    public double WaterUsage { get; set; }

    [MaxLength(500)]
    public string? WaterMeterImageUrl { get; set; }

    // ===== Người ghi =====
    public string RecordedById { get; set; } = "";
    public ApplicationUser RecordedBy { get; set; } = null!;

    [MaxLength(500)]
    public string? Note { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}