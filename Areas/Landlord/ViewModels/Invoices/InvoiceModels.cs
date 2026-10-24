using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Helpers;
using System.ComponentModel.DataAnnotations;
using EZ_Rent.Services.Billing;
using EZ_Rent.Services.Payments;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Landlord.ViewModels.Invoices;

public sealed class InvoiceListInput
{
    [RegularExpression("^(open|awaiting|overdue|paid|cancelled)$")] public string? Status { get; set; }
    [RegularExpression(@"^\d{4}-\d{2}$")] public string? Period { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class InvoiceRow
{
    public int Id { get; init; }
    public string Period { get; init; } = "";
    public string RoomTitle { get; init; } = "";
    public string TenantName { get; init; } = "";
    public decimal Total { get; init; }
    public DateTime DueDate { get; init; }
    public PaymentStatus Status { get; init; }
    public DateTime? TenantConfirmedAt { get; init; }
    public bool Overdue { get; init; }
}

public sealed class InvoiceListModel
{
    public InvoiceListInput Input { get; init; } = new();
    public PaginatedList<InvoiceRow> Items { get; init; } = EmptyPage.Of<InvoiceRow>();
    public PaginationInfo Pager { get; init; } = new();
    public Dictionary<string, int> Counts { get; init; } = new();
    public decimal OpenAmount { get; init; }
    public bool InvalidQuery { get; init; }
}

public sealed class InvoiceInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn hợp đồng.")]
    [Display(Name = "Hợp đồng")]
    public int RentalContractId { get; set; }

    [Required(ErrorMessage = "Chọn kỳ thanh toán.")]
    [RegularExpression(@"^\d{4}-\d{2}$", ErrorMessage = "Kỳ có dạng yyyy-MM.")]
    [Display(Name = "Kỳ thanh toán")]
    public string BillingPeriod { get; set; } = "";

    [Display(Name = "Tính phí dịch vụ chung của phòng")]
    public bool IncludeFixedFee { get; set; } = true;

    /// <summary>Số lượng theo từng dịch vụ của phòng (khóa = ServiceId). 0 = không tính.</summary>
    public Dictionary<int, int> Qty { get; set; } = new();

    [Range(typeof(decimal), "-100000000", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Khoản điều chỉnh từ -100.000.000 đến 100.000.000 đ.")]
    [Display(Name = "Điều chỉnh / phát sinh (đ)")]
    public decimal? AdjustmentAmount { get; set; } = 0;

    [Display(Name = "Lập hóa đơn không kèm điện nước")]
    public bool WithoutMeter { get; set; }

    [Required(ErrorMessage = "Chọn hạn thanh toán.")]
    [DataType(DataType.Date)] [Display(Name = "Hạn thanh toán")]
    public DateTime? DueDate { get; set; }

    [StringLength(250, ErrorMessage = "Ghi chú tối đa 250 ký tự.")]
    [Display(Name = "Ghi chú cho người thuê")]
    public string? Note { get; set; }
}

public sealed record ServiceOption(int Id, string Name, decimal UnitPrice, string Unit);

public sealed class InvoiceFormModel
{
    public int? Id { get; init; }
    public InvoiceInput Input { get; init; } = new();
    public IReadOnlyList<OptionItem> Contracts { get; init; } = [];
    public IReadOnlyList<ServiceOption> Services { get; init; } = [];
    public decimal FixedFee { get; init; }
    public InvoiceBreakdown? Breakdown { get; init; }
    public int? MeterReadingId { get; init; }
    public string? ContractSummary { get; init; }
    public bool BankConfigured { get; init; }
    /// <summary>Phiếu chỉ số của kỳ đã có đủ ảnh công tơ điện và đồng hồ nước để lưu đối chiếu khi phát hành.</summary>
    public bool MeterPhotosReady { get; init; }
}

public sealed class InvoiceDetailModel
{
    public int Id { get; init; }
    public string Period { get; init; } = "";
    public PaymentStatus Status { get; init; }
    public bool Overdue { get; init; }
    public int ContractId { get; init; }
    public string RoomTitle { get; init; } = "";
    public string RoomAddress { get; init; } = "";
    public string TenantName { get; init; } = "";
    public string LandlordName { get; init; } = "";
    public decimal RoomRent { get; init; }
    public double ElectricUsage { get; init; }
    public decimal ElectricAmount { get; init; }
    public double WaterUsage { get; init; }
    public decimal WaterAmount { get; init; }
    public decimal ServiceFee { get; init; }
    public decimal Adjustment { get; init; }
    public decimal Total { get; init; }
    public decimal ElectricPrice { get; init; }
    public decimal WaterPrice { get; init; }
    public DateTime IssueDate { get; init; }
    public DateTime DueDate { get; init; }
    public DateTime? TenantConfirmedAt { get; init; }
    public DateTime? PaidAt { get; init; }
    public string? Note { get; init; }
    public bool HasQr { get; init; }
    public string? QrDataUri { get; init; }
    public string? TransferNote { get; init; }
    public BankAccount? Bank { get; init; }
    public int? MeterReadingId { get; init; }
    public double? PrevElectric { get; init; }
    public double? CurrElectric { get; init; }
    public double? PrevWater { get; init; }
    public double? CurrWater { get; init; }
    /// <summary>Bộ chỉ số + 2 ảnh đã lưu riêng cho hóa đơn (InvoiceMeterEvidence), người thuê đối chiếu bằng chính bộ này.</summary>
    public bool HasEvidence { get; init; }
    public DateTime? EvidenceCapturedAt { get; init; }
    public bool CanEdit => Status == PaymentStatus.Unpaid && TenantConfirmedAt == null;
    public bool CanMarkPaid => Status is PaymentStatus.Unpaid or PaymentStatus.AwaitingConfirmation or PaymentStatus.Overdue;
    public bool CanCancel => Status is PaymentStatus.Unpaid or PaymentStatus.Overdue && TenantConfirmedAt == null;
}
