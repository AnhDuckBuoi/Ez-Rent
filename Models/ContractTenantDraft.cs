using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Models;

public enum IdentityDocumentKind
{
    [Display(Name = "CCCD / Căn cước")] CitizenIdentity = 1,
    [Display(Name = "CMND")] LegacyIdentity = 2,
    [Display(Name = "Hộ chiếu")] Passport = 3
}

/// <summary>Append-only draft snapshot. It never changes a retained document or signing state.</summary>
public class ContractTenantDraft
{
    public int ContractTenantDraftId { get; set; }
    public int RentalContractId { get; set; }
    public RentalContract RentalContract { get; set; } = null!;
    [MaxLength(450)] public string TenantId { get; set; } = "";
    public int Revision { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    [MaxLength(40)] public string TemplateVersion { get; set; } = "BM03-m3-v1";
    [MaxLength(64)] public string ContractStamp { get; set; } = "";

    [MaxLength(80)] public string FullName { get; set; } = "";
    [MaxLength(20)] public string? PhoneNumber { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    [MaxLength(500)] public string? PermanentAddress { get; set; }
    public IdentityDocumentKind? DocumentKind { get; set; }
    [MaxLength(30)] public string? DocumentNumber { get; set; }
    public DateOnly? DocumentIssuedOn { get; set; }
    [MaxLength(200)] public string? DocumentIssuedAt { get; set; }

    // Server-supplied values are copied at save time, not accepted from Tenant form fields.
    [MaxLength(80)] public string LandlordName { get; set; } = "";
    public string? LandlordPhone { get; set; }
    [MaxLength(700)] public string RoomAddress { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal DepositAmount { get; set; }
    public decimal ElectricityPrice { get; set; }
    public decimal WaterPrice { get; set; }
    public string? ContractTerms { get; set; }
}
