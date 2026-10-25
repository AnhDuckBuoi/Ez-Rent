using System.ComponentModel.DataAnnotations;
using System.Globalization;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Routing;
using EZ_Rent.Services.Discovery;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public class RentalSearchInput : IValidatableObject
{
    [StringLength(100, ErrorMessage = "Từ khóa tối đa 100 ký tự.")] public string? Keyword { get; set; }
    [StringLength(100, ErrorMessage = "Tên thành phố tối đa 100 ký tự.")] public string? City { get; set; }
    [StringLength(100, ErrorMessage = "Tên quận/huyện tối đa 100 ký tự.")] public string? District { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Giá phải là số không âm và nằm trong giới hạn cho phép.")] public decimal? MinPrice { get; set; }
    [Range(typeof(decimal), "0", "9999999999999999", ErrorMessage = "Giá phải là số không âm và nằm trong giới hạn cho phép.")] public decimal? MaxPrice { get; set; }
    [Range(0d, 1000000d, ErrorMessage = "Diện tích phải từ 0 đến 1.000.000 m².")] public double? MinArea { get; set; }
    [Range(0d, 1000000d, ErrorMessage = "Diện tích phải từ 0 đến 1.000.000 m².")] public double? MaxArea { get; set; }
    [EnumDataType(typeof(RoomType), ErrorMessage = "Loại phòng không hợp lệ.")] public RoomType? RoomType { get; set; }
    [StringLength(500, ErrorMessage = "Bộ lọc tiện ích quá dài.")] public string? Amenities { get; set; }
    public string[] AmenityValues => (Amenities ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct().ToArray();
    public string Sort { get; set; } = "newest";
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải là số nguyên dương.")] public int Page { get; set; } = 1;

    public void Normalize()
    {
        Keyword = Keyword?.Trim(); City = City?.Trim(); District = District?.Trim();
        Amenities = string.Join(",", AmenityValues);
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinPrice > MaxPrice) yield return new("Giá tối thiểu phải nhỏ hơn hoặc bằng giá tối đa.", [nameof(MinPrice)]);
        if (MinArea > MaxArea) yield return new("Diện tích tối thiểu phải nhỏ hơn hoặc bằng diện tích tối đa.", [nameof(MinArea)]);
        if (AmenityValues.Length > 10 || AmenityValues.Any(value => value.Length > 60))
            yield return new("Chọn tối đa 10 tiện ích hợp lệ.", [nameof(Amenities)]);
        if ((MinArea.HasValue && !double.IsFinite(MinArea.Value)) || (MaxArea.HasValue && !double.IsFinite(MaxArea.Value)))
            yield return new("Diện tích phải là số hữu hạn.", [nameof(MinArea)]);
        if (!new[] { "newest", "price-asc", "price-desc", "area-asc", "area-desc" }.Contains(Sort))
            yield return new("Cách sắp xếp không hợp lệ.", [nameof(Sort)]);
    }

    public RouteValueDictionary RouteValues(int? page = null) => new()
    {
        ["Keyword"] = Keyword, ["City"] = City, ["District"] = District,
        ["MinPrice"] = MinPrice?.ToString(CultureInfo.InvariantCulture), ["MaxPrice"] = MaxPrice?.ToString(CultureInfo.InvariantCulture),
        ["MinArea"] = MinArea?.ToString(CultureInfo.InvariantCulture), ["MaxArea"] = MaxArea?.ToString(CultureInfo.InvariantCulture),
        ["RoomType"] = RoomType.HasValue ? ((int)RoomType.Value).ToString() : null,
        ["Sort"] = Sort, ["Page"] = page ?? Page, ["Amenities"] = string.IsNullOrEmpty(Amenities) ? null : Amenities
    };

    public RentalSearchCriteria Criteria() => new()
    {
        Keyword = Keyword, City = City, District = District, MinPrice = MinPrice, MaxPrice = MaxPrice,
        MinArea = MinArea, MaxArea = MaxArea, RoomType = RoomType, Amenities = AmenityValues
    };

    public List<RentalFilterChip> ActiveFilters()
    {
        var chips = new List<RentalFilterChip>();
        var culture = CultureInfo.GetCultureInfo("vi-VN");
        if (!string.IsNullOrWhiteSpace(Keyword)) chips.Add(new($"Từ khóa: {Keyword}", [nameof(Keyword)]));
        if (!string.IsNullOrWhiteSpace(City)) chips.Add(new(City, [nameof(City), nameof(District)]));
        if (!string.IsNullOrWhiteSpace(District)) chips.Add(new(District, [nameof(District)]));
        if (AmenityValues.Length > 0) chips.Add(new("Tiện ích: " + string.Join(", ", AmenityValues), [nameof(Amenities)]));
        if (MinPrice.HasValue || MaxPrice.HasValue)
        {
            var price = MinPrice.HasValue && MaxPrice.HasValue ? $"{MinPrice.Value.ToString("N0", culture)}–{MaxPrice.Value.ToString("N0", culture)} đ"
                : MinPrice.HasValue ? $"Từ {MinPrice.Value.ToString("N0", culture)} đ" : $"Đến {MaxPrice!.Value.ToString("N0", culture)} đ";
            chips.Add(new(price, [nameof(MinPrice), nameof(MaxPrice)]));
        }
        if (MinArea.HasValue || MaxArea.HasValue)
        {
            var area = MinArea.HasValue && MaxArea.HasValue ? $"{MinArea.Value.ToString("0.##", culture)}–{MaxArea.Value.ToString("0.##", culture)} m²"
                : MinArea.HasValue ? $"Từ {MinArea.Value.ToString("0.##", culture)} m²" : $"Đến {MaxArea!.Value.ToString("0.##", culture)} m²";
            chips.Add(new(area, [nameof(MinArea), nameof(MaxArea)]));
        }
        if (RoomType.HasValue && Enum.IsDefined(RoomType.Value))
            chips.Add(new(RentalPresentation.RoomTypeName(RoomType.Value), [nameof(RoomType)]));
        return chips;
    }

    public RouteValueDictionary WithoutFilter(IEnumerable<string> keys)
    {
        var values = RouteValues(1);
        foreach (var key in keys) values.Remove(key);
        return values;
    }
}

public record RentalFilterChip(string Label, string[] Keys);

public class RentalCardModel
{
    public int RentalPostId { get; set; }
    public string Title { get; set; } = "";
    public string Address { get; set; } = "";
    public decimal Price { get; set; }
    public double Area { get; set; }
    public RoomType RoomType { get; set; }
    public string ImageUrl { get; set; } = "/images/room-placeholder.svg";
    public bool IsSaved { get; set; }
    public bool IsAvailable { get; set; } = true;
    public string ReturnUrl { get; set; } = "/Tenant/RentalPost";
}

public class RentalSearchPageModel
{
    public IReadOnlyList<RentalCardModel> Recommendations { get; set; } = [];
    public RentalSearchInput Filters { get; set; } = new();
    public List<RentalCardModel> Rooms { get; set; } = [];
    public List<string> Cities { get; set; } = [];
    public List<string> Districts { get; set; } = [];
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (TotalCount - 1) / 12 + 1);
    public bool IsFavorites { get; set; }
    public string CurrentUrl { get; set; } = "/Tenant/RentalPost";
}

public class RentalDetailModel
{
    public RentalCardModel Room { get; set; } = new();
    public string Description { get; set; } = "";
    public List<string> Images { get; set; } = [];
    public List<string> Amenities { get; set; } = [];
    public decimal ElectricityPrice { get; set; }
    public decimal WaterPrice { get; set; }
    public decimal ServiceFee { get; set; }
    public List<RentalServiceModel> Services { get; set; } = [];
    public string OwnerName { get; set; } = "";
    public string? OwnerAvatar { get; set; }
    public string BackUrl { get; set; } = "/Tenant/RentalPost";
    public string BackLabel { get; set; } = "Quay lại danh sách";
}

public record RentalServiceModel(string Name, decimal UnitPrice, string Unit);

public class SetFavoriteInput
{
    [Range(1, int.MaxValue)] public int RentalPostId { get; set; }
    [Required] public bool? Saved { get; set; }
    [StringLength(2048)] public string? ReturnUrl { get; set; }
}
