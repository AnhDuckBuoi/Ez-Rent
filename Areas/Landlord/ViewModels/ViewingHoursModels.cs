using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Landlord.ViewModels;

public sealed class ViewingHoursInput
{
    /// <summary>Khóa 0..6 (DayOfWeek); giá trị là chuỗi giờ "08:00, 09:30".</summary>
    public Dictionary<int, string?> Weekly { get; set; } = new();
}

public sealed class ViewingExceptionInput
{
    [Required(ErrorMessage = "Chọn ngày.")]
    [DataType(DataType.Date)]
    [Display(Name = "Ngày")]
    public DateTime? Date { get; set; }

    [RegularExpression("^(closed|custom)$")]
    public string Mode { get; set; } = "closed";

    [StringLength(200)]
    [Display(Name = "Giờ đến riêng cho ngày này")]
    public string? Times { get; set; }
}

public sealed record DaySlotPreview(TimeOnly Time, string State);
public sealed record DayPreview(DateOnly Date, bool IsException, IReadOnlyList<DaySlotPreview> Slots);

public sealed class ViewingHoursModel
{
    public ViewingHoursInput Input { get; init; } = new();
    public ViewingExceptionInput Exception { get; init; } = new();
    public IReadOnlyList<(DateOnly Date, IReadOnlyList<string> Times)> Exceptions { get; init; } = [];
    public IReadOnlyList<DayPreview> Week { get; init; } = [];
    public bool Configured { get; init; }
    public DateOnly Today { get; init; }
}
