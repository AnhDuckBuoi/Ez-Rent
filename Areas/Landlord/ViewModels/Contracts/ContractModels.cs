using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Helpers;
using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Landlord.ViewModels.Contracts;

public sealed class ContractListInput
{
    [EnumDataType(typeof(ContractStatus))] public ContractStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class ContractRow
{
    public int Id { get; init; }
    public int RoomId { get; init; }
    public string RoomTitle { get; init; } = "";
    public string TenantName { get; init; } = "";
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal MonthlyRent { get; init; }
    public ContractStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}

public sealed class ContractListModel
{
    public ContractListInput Input { get; init; } = new();
    public PaginatedList<ContractRow> Items { get; init; } = EmptyPage.Of<ContractRow>();
    public PaginationInfo Pager { get; init; } = new();
    public Dictionary<ContractStatus, int> Counts { get; init; } = new();
    public bool InvalidQuery { get; init; }
}

public sealed class ContractInput
{
    [Range(1, int.MaxValue, ErrorMessage = "Chọn phòng cho thuê.")]
    [Display(Name = "Phòng")]
    public int RentalPostId { get; set; }

    [Display(Name = "Người thuê")]
    public string? TenantId { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    [Display(Name = "Hoặc email tài khoản người thuê")]
    public string? TenantEmail { get; set; }

    [Required(ErrorMessage = "Chọn ngày bắt đầu.")]
    [DataType(DataType.Date)] [Display(Name = "Ngày bắt đầu")]
    public DateTime? StartDate { get; set; }

    [Required(ErrorMessage = "Chọn ngày kết thúc.")]
    [DataType(DataType.Date)] [Display(Name = "Ngày kết thúc")]
    public DateTime? EndDate { get; set; }

    [Required(ErrorMessage = "Nhập tiền thuê.")]
    [Range(typeof(decimal), "100000", "1000000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Tiền thuê từ 100.000 đến 1.000.000.000 đ/tháng.")]
    [Display(Name = "Tiền thuê (đ/tháng)")]
    public decimal? MonthlyRent { get; set; }

    [Required(ErrorMessage = "Nhập tiền đặt cọc (có thể là 0).")]
    [Range(typeof(decimal), "0", "3000000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Tiền cọc từ 0 đến 3.000.000.000 đ.")]
    [Display(Name = "Tiền đặt cọc (đ)")]
    public decimal? DepositAmount { get; set; }

    [Required(ErrorMessage = "Nhập giá điện.")]
    [Range(typeof(decimal), "0", "20000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Giá điện từ 0 đến 20.000 đ/kWh.")]
    [Display(Name = "Giá điện (đ/kWh)")]
    public decimal? AgreedElectricityPrice { get; set; }

    [Required(ErrorMessage = "Nhập giá nước.")]
    [Range(typeof(decimal), "0", "500000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Giá nước từ 0 đến 500.000 đ/m³.")]
    [Display(Name = "Giá nước (đ/m³)")]
    public decimal? AgreedWaterPrice { get; set; }

    [RegularExpression("^(transfer|cash|both)$", ErrorMessage = "Chọn hình thức thanh toán.")]
    [Display(Name = "Hình thức thanh toán")]
    public string PaymentMethod { get; set; } = "both";

    [StringLength(150, ErrorMessage = "Nơi lập hợp đồng tối đa 150 ký tự.")]
    [Display(Name = "Nơi lập hợp đồng")]
    public string? SigningPlace { get; set; }

    [Required(ErrorMessage = "Nhập điều khoản hợp đồng.")]
    [StringLength(8000, MinimumLength = 30, ErrorMessage = "Điều khoản dài 30–8000 ký tự.")]
    [Display(Name = "Điều khoản và trách nhiệm hai bên")]
    public string Terms { get; set; } = "";
}


public sealed class ContractFormModel
{
    public int? Id { get; init; }
    public ContractInput Input { get; init; } = new();
    public IReadOnlyList<OptionItem> Rooms { get; init; } = [];
    public IReadOnlyList<OptionItem> Tenants { get; init; } = [];
    public string? TenantName { get; init; }
}

public sealed class ContractDetailModel
{
    public int Id { get; init; }
    public ContractStatus Status { get; init; }
    public int RoomId { get; init; }
    public string RoomTitle { get; init; } = "";
    public string RoomAddress { get; init; } = "";
    public double RoomArea { get; init; }
    public string LandlordName { get; init; } = "";
    public string? LandlordPhone { get; init; }
    public string? LandlordEmail { get; init; }
    public string TenantId { get; init; } = "";
    public string TenantName { get; init; } = "";
    public string? TenantPhone { get; init; }
    public string? TenantEmail { get; init; }
    public DateTime StartDate { get; init; }
    public DateTime EndDate { get; init; }
    public decimal MonthlyRent { get; init; }
    public decimal DepositAmount { get; init; }
    public decimal AgreedElectricityPrice { get; init; }
    public decimal AgreedWaterPrice { get; init; }
    public string Terms { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime? LandlordConfirmedAt { get; init; }
    public DateTime? TenantConfirmedAt { get; init; }
    public int InvoiceCount { get; init; }
    public int UnpaidInvoices { get; init; }
    public string PaymentMethodLabel { get; init; } = "";
    public string? SigningPlace { get; init; }
    public int CurrentDocumentVersion { get; init; }
    public IReadOnlyList<ContractDocumentRow> Documents { get; init; } = [];
    public TenantDraftView? TenantDraft { get; init; }
    public EZ_Rent.Services.Contracts.LandlordLegalProfile? LandlordLegal { get; init; }
    /// <summary>Người thuê đã đối chiếu đúng phiên bản tài liệu hiện tại (Tenant chưa có nút ký trên web).</summary>
    public DateTime? CurrentVersionAcknowledgedAt => Documents.FirstOrDefault(d => d.Version == CurrentDocumentVersion)?.TenantAcknowledgedAt;
    public bool TenantAgreed => TenantConfirmedAt.HasValue || CurrentVersionAcknowledgedAt.HasValue;
}

public sealed record ContractDocumentRow(int Id, int Version, string FileName, string ContentType, long ByteLength, DateTime CreatedAt, DateTime? TenantAcknowledgedAt);

/// <summary>Bản nháp mới nhất người thuê tự khai cho BM03 (chỉ đọc, chưa xác minh).</summary>
public sealed record TenantDraftView(int Revision, DateTime CreatedAt, string FullName, string? PhoneNumber, DateOnly? DateOfBirth,
    string? PermanentAddress, string? DocumentKind, string? DocumentNumber, DateOnly? DocumentIssuedOn, string? DocumentIssuedAt);
