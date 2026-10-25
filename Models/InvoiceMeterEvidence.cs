using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Models;

/// <summary>One captured set of meter indices and private files for an issued invoice.</summary>
public class InvoiceMeterEvidence
{
    public int InvoiceMeterEvidenceId { get; set; }
    public int InvoiceId { get; set; }
    public Invoice Invoice { get; set; } = null!;
    public int RentalPostId { get; set; }
    [MaxLength(450)] public string TenantId { get; set; } = "";
    [MaxLength(7)] public string Period { get; set; } = "";
    public DateTime ReadingDate { get; set; }
    public double PrevElectricIndex { get; set; }
    public double CurrElectricIndex { get; set; }
    public double ElectricUsage { get; set; }
    public decimal ElectricityPrice { get; set; }
    public double PrevWaterIndex { get; set; }
    public double CurrWaterIndex { get; set; }
    public double WaterUsage { get; set; }
    public decimal WaterPrice { get; set; }
    [MaxLength(80)] public string? ElectricStorageKey { get; set; }
    [MaxLength(80)] public string? WaterStorageKey { get; set; }
    [MaxLength(80)] public string? ElectricContentType { get; set; }
    [MaxLength(80)] public string? WaterContentType { get; set; }
    [MaxLength(64)] public string? ElectricSha256 { get; set; }
    [MaxLength(64)] public string? WaterSha256 { get; set; }
    [MaxLength(450)] public string CapturedById { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
