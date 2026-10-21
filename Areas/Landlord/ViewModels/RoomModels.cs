using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Http;

namespace EZ_Rent.Areas.Landlord.ViewModels;

public sealed class RoomListInput
{
    [EnumDataType(typeof(ApprovalStatus))] public ApprovalStatus? Approval { get; set; }
    [EnumDataType(typeof(RoomStatus))] public RoomStatus? Room { get; set; }
    [StringLength(100)] public string? Q { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class RoomRow
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Address { get; init; } = "";
    public decimal Price { get; init; }
    public double Area { get; init; }
    public RoomType RoomType { get; init; }
    public ApprovalStatus Approval { get; init; }
    public RoomStatus Room { get; init; }
    public string? ApprovalNote { get; init; }
    public string? ImageUrl { get; init; }
    public int PendingViewings { get; init; }
    public int Favorites { get; init; }
    public string? TenantName { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public sealed class RoomListModel
{
    public RoomListInput Input { get; init; } = new();
    public PagedList<RoomRow> Rooms { get; init; } = new();
    public Dictionary<ApprovalStatus, int> Counts { get; init; } = new();
    public int Total => Counts.Values.Sum();
    public bool InvalidQuery { get; init; }
}

public sealed class ServiceInput
{
    [StringLength(100, ErrorMessage = "Tên dịch vụ tối đa 100 ký tự.")]
    public string? Name { get; set; }

    [Range(typeof(decimal), "0", "100000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Đơn giá dịch vụ từ 0 đến 100.000.000 đ.")]
    public decimal? UnitPrice { get; set; }

    [StringLength(30, ErrorMessage = "Đơn vị tối đa 30 ký tự.")]
    public string? Unit { get; set; }
}

/// <summary>Chỉ những trường chủ trọ được nhập. Chủ sở hữu, trạng thái duyệt và thời gian do server đặt.</summary>
public sealed class RoomInput
{
    [Required(ErrorMessage = "Nhập tiêu đề tin.")]
    [StringLength(200, MinimumLength = 10, ErrorMessage = "Tiêu đề dài 10–200 ký tự.")]
    [Display(Name = "Tiêu đề tin")]
    public string Title { get; set; } = "";

    [Required(ErrorMessage = "Mô tả giúp người thuê hiểu phòng trước khi đến xem.")]
    [StringLength(4000, MinimumLength = 30, ErrorMessage = "Mô tả dài 30–4000 ký tự.")]
    [Display(Name = "Mô tả")]
    public string Description { get; set; } = "";

    [EnumDataType(typeof(RoomType), ErrorMessage = "Chọn loại phòng.")]
    [Display(Name = "Loại phòng")]
    public RoomType RoomType { get; set; } = RoomType.Room;

    [Required(ErrorMessage = "Nhập giá thuê.")]
    [Range(typeof(decimal), "100000", "1000000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Giá thuê từ 100.000 đến 1.000.000.000 đ/tháng.")]
    [Display(Name = "Giá thuê (đ/tháng)")]
    public decimal? Price { get; set; }

    // Nhận dạng chuỗi rồi tự đọc theo InvariantCulture: máy chủ tiếng Việt coi "25.5" là 255.
    [Required(ErrorMessage = "Nhập diện tích.")]
    [RegularExpression(@"^\d{1,4}([.,]\d{1,2})?$", ErrorMessage = "Diện tích là số, tối đa 2 chữ số thập phân (ví dụ 25.5).")]
    [Display(Name = "Diện tích (m²)")]
    public string? Area { get; set; }

    [Required(ErrorMessage = "Nhập số nhà, tên đường.")]
    [StringLength(255, ErrorMessage = "Địa chỉ tối đa 255 ký tự.")]
    [Display(Name = "Số nhà, đường")]
    public string Address { get; set; } = "";

    [StringLength(100)] [Display(Name = "Phường/Xã")]
    public string? Ward { get; set; }

    [Required(ErrorMessage = "Nhập quận/huyện.")]
    [StringLength(100)] [Display(Name = "Quận/Huyện")]
    public string District { get; set; } = "";

    [Required(ErrorMessage = "Nhập tỉnh/thành phố.")]
    [StringLength(100)] [Display(Name = "Tỉnh/Thành phố")]
    public string City { get; set; } = "Hà Nội";

    [StringLength(500, ErrorMessage = "Vị trí bản đồ tối đa 500 ký tự.")]
    [Display(Name = "Vị trí trên bản đồ")]
    public string? MapLocation { get; set; }

    [Display(Name = "Tiện ích")]
    public List<string> Amenities { get; set; } = [];

    [Required(ErrorMessage = "Nhập giá điện.")]
    [Range(typeof(decimal), "0", "20000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Giá điện từ 0 đến 20.000 đ/kWh.")]
    [Display(Name = "Giá điện (đ/kWh)")]
    public decimal? ElectricityUnitPrice { get; set; } = 3500;

    [Required(ErrorMessage = "Nhập giá nước.")]
    [Range(typeof(decimal), "0", "500000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Giá nước từ 0 đến 500.000 đ/m³.")]
    [Display(Name = "Giá nước (đ/m³)")]
    public decimal? WaterUnitPrice { get; set; } = 25000;

    [Range(typeof(decimal), "0", "10000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Phí dịch vụ chung từ 0 đến 10.000.000 đ/tháng.")]
    [Display(Name = "Phí dịch vụ chung (đ/tháng)")]
    public decimal? ServiceFee { get; set; } = 0;

    [Required(ErrorMessage = "Chọn ngày có thể vào ở.")]
    [DataType(DataType.Date)]
    [Display(Name = "Có thể vào ở từ")]
    public DateTime? AvailableFrom { get; set; }

    /// <summary>BM01 tách riêng "Phí Internet"; lưu thành dịch vụ tên "Internet" (đ/tháng) để hóa đơn tính được.</summary>
    [Range(typeof(decimal), "0", "2000000", ParseLimitsInInvariantCulture = true, ConvertValueInInvariantCulture = true,
        ErrorMessage = "Phí Internet từ 0 đến 2.000.000 đ/tháng.")]
    [Display(Name = "Phí Internet (đ/tháng)")]
    public decimal? InternetFee { get; set; }

    public List<ServiceInput> Services { get; set; } = [];

    [Display(Name = "Ảnh/video phòng")]
    public List<IFormFile> Media { get; set; } = [];
}

public sealed class RoomFormModel
{
    public int? Id { get; init; }
    public RoomInput Input { get; init; } = new();
    public ApprovalStatus? CurrentApproval { get; init; }
    public string? ApprovalNote { get; init; }
}

public sealed record RoomMediaItem(int Id, string Url, MediaType Type, bool IsPrimary, int Order);
public sealed record RoomServiceItem(string Name, decimal UnitPrice, string Unit);

public sealed class RoomDetailModel
{
    public int Id { get; init; }
    public string Title { get; init; } = "";
    public string Description { get; init; } = "";
    public RoomType RoomType { get; init; }
    public decimal Price { get; init; }
    public double Area { get; init; }
    public string Address { get; init; } = "";
    public string Ward { get; init; } = "";
    public string District { get; init; } = "";
    public string City { get; init; } = "";
    public string? MapLocation { get; init; }
    public string? MapUrl { get; init; }
    public List<string> Amenities { get; init; } = [];
    public decimal ElectricityUnitPrice { get; init; }
    public decimal WaterUnitPrice { get; init; }
    public decimal ServiceFee { get; init; }
    public DateTime AvailableFrom { get; init; }
    public ApprovalStatus Approval { get; init; }
    public RoomStatus Room { get; init; }
    public string? ApprovalNote { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public IReadOnlyList<RoomMediaItem> Media { get; init; } = [];
    public IReadOnlyList<RoomServiceItem> Services { get; init; } = [];
    public int PendingViewings { get; init; }
    public int Favorites { get; init; }
    public int UpcomingViewings { get; init; }
    public int OpenRepairs { get; init; }
    public int? ActiveContractId { get; init; }
    public string? TenantName { get; init; }
    public bool CanDelete { get; init; }
    public string FullAddress => string.Join(", ", new[] { Address, Ward, District, City }.Where(x => !string.IsNullOrWhiteSpace(x)));
}
