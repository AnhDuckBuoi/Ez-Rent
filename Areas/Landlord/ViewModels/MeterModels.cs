using System.ComponentModel.DataAnnotations;
using System.Globalization;
using Microsoft.AspNetCore.Http;

namespace EZ_Rent.Areas.Landlord.ViewModels;

public sealed class MeterIndexInput
{
    [RegularExpression(@"^\d{4}-\d{2}$")] public string? Period { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class MeterRoomRow
{
    public int RoomId { get; init; }
    public string RoomTitle { get; init; } = "";
    public string TenantName { get; init; } = "";
    public int? ContractId { get; init; }
    public int? ReadingId { get; init; }
    public double? ElectricUsage { get; init; }
    public double? WaterUsage { get; init; }
    public string? LastPeriod { get; init; }
    public bool HasInvoice { get; init; }
}

public sealed class MeterIndexModel
{
    public string Period { get; init; } = "";
    public string CurrentPeriod { get; init; } = "";
    public PagedList<MeterRoomRow> Rooms { get; init; } = new();
    public int Recorded { get; init; }
    public bool InvalidQuery { get; init; }
}

public sealed class MeterHistoryRow
{
    public int Id { get; init; }
    public string Period { get; init; } = "";
    public double PrevElectric { get; init; }
    public double CurrElectric { get; init; }
    public double ElectricUsage { get; init; }
    public double PrevWater { get; init; }
    public double CurrWater { get; init; }
    public double WaterUsage { get; init; }
    public DateTime ReadingDate { get; init; }
    public bool HasPhotos { get; init; }
    public int? InvoiceId { get; init; }
}

public sealed class MeterHistoryModel
{
    public int RoomId { get; init; }
    public string RoomTitle { get; init; } = "";
    public PagedList<MeterHistoryRow> Items { get; init; } = new();
}

/// <summary>Chỉ số nhận dạng chuỗi và đọc bằng InvariantCulture để "123.5" không thành 1235 trên máy tiếng Việt.</summary>
public sealed class MeterInput
{
    private const string NumberPattern = @"^\d{1,7}([.,]\d{1,2})?$";
    private const string NumberError = "Nhập số không âm, tối đa 2 chữ số thập phân.";

    [Range(1, int.MaxValue, ErrorMessage = "Chọn phòng.")]
    public int RentalPostId { get; set; }

    [Required(ErrorMessage = "Chọn kỳ ghi nhận.")]
    [RegularExpression(@"^\d{4}-\d{2}$", ErrorMessage = "Kỳ có dạng yyyy-MM.")]
    [Display(Name = "Kỳ ghi nhận")]
    public string Period { get; set; } = "";

    [RegularExpression(NumberPattern, ErrorMessage = NumberError)] [Display(Name = "Chỉ số điện kỳ trước")]
    public string? PrevElectricIndex { get; set; }

    [Required(ErrorMessage = "Nhập chỉ số điện hiện tại.")]
    [RegularExpression(NumberPattern, ErrorMessage = NumberError)] [Display(Name = "Chỉ số điện hiện tại")]
    public string? CurrElectricIndex { get; set; }

    [RegularExpression(NumberPattern, ErrorMessage = NumberError)] [Display(Name = "Chỉ số nước kỳ trước")]
    public string? PrevWaterIndex { get; set; }

    [Required(ErrorMessage = "Nhập chỉ số nước hiện tại.")]
    [RegularExpression(NumberPattern, ErrorMessage = NumberError)] [Display(Name = "Chỉ số nước hiện tại")]
    public string? CurrWaterIndex { get; set; }

    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")] [Display(Name = "Ghi chú")]
    public string? Note { get; set; }

    [Display(Name = "Ảnh công tơ điện")] public IFormFile? ElectricPhoto { get; set; }
    [Display(Name = "Ảnh công tơ nước")] public IFormFile? WaterPhoto { get; set; }

    public static bool TryRead(string? text, out double value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(text) &&
            double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value) &&
            value >= 0 && value < 10_000_000;
    }

    public static string Show(double value) => value.ToString("0.##", CultureInfo.InvariantCulture);
}

public sealed class MeterFormModel
{
    public int? Id { get; init; }
    public MeterInput Input { get; init; } = new();
    public string RoomTitle { get; init; } = "";
    public string TenantName { get; init; } = "";
    public decimal ElectricPrice { get; init; }
    public decimal WaterPrice { get; init; }
    /// <summary>Có kỳ trước: chỉ số kỳ trước khóa theo dữ liệu đã lưu. Không có: chủ trọ nhập chỉ số đầu.</summary>
    public bool HasPrevious { get; init; }
    public string? PreviousPeriod { get; init; }
    public string? ElectricPhotoUrl { get; init; }
    public string? WaterPhotoUrl { get; init; }
}

public sealed class MeterDetailModel
{
    public int Id { get; init; }
    public int RoomId { get; init; }
    public string RoomTitle { get; init; } = "";
    public string Period { get; init; } = "";
    public DateTime ReadingDate { get; init; }
    public double PrevElectric { get; init; }
    public double CurrElectric { get; init; }
    public double ElectricUsage { get; init; }
    public double PrevWater { get; init; }
    public double CurrWater { get; init; }
    public double WaterUsage { get; init; }
    public string? ElectricPhotoUrl { get; init; }
    public string? WaterPhotoUrl { get; init; }
    public string? Note { get; init; }
    public string RecordedBy { get; init; } = "";
    public int? InvoiceId { get; init; }
    public bool IsLatest { get; init; }
    public decimal? ElectricPrice { get; init; }
    public decimal? WaterPrice { get; init; }
}
