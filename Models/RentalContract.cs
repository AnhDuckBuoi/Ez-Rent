using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Models;

public class RentalContract
{
    public int RentalContractId { get; set; }

    public int RentalPostId { get; set; }
    public RentalPost RentalPost { get; set; } = null!;

    public string TenantId { get; set; } = "";
    public ApplicationUser Tenant { get; set; } = null!;

    public string LandlordId { get; set; } = "";
    public ApplicationUser Landlord { get; set; } = null!;

    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal MonthlyRent { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal DepositAmount { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AgreedElectricityPrice { get; set; }

    [Column(TypeName = "decimal(18,2)")]
    public decimal AgreedWaterPrice { get; set; }

    public string? Terms { get; set; }

    public ContractStatus Status { get; set; } = ContractStatus.Draft;

    public DateTime? TenantConfirmedAt { get; set; }
    public DateTime? LandlordConfirmedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}