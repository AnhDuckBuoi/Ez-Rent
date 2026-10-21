using System.Globalization;
using EZ_Rent.Areas.Tenant.Services;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed class ViewingBookingModel
{
    public required RentalDetailModel Detail { get; init; }
    public DateOnly Today { get; init; }
    public DateOnly Date { get; init; }
    public DateOnly WeekStart => Today.AddDays((Date.DayNumber - Today.DayNumber) / 7 * 7);
    public IEnumerable<DateOnly> Dates => Enumerable.Range(0, Math.Min(7, DateOnly.MaxValue.DayNumber - WeekStart.DayNumber + 1)).Select(WeekStart.AddDays);
    public DateOnly? PreviousWeek => WeekStart > Today ? WeekStart.AddDays(-7) : null;
    public DateOnly? NextWeek => WeekStart.DayNumber <= DateOnly.MaxValue.DayNumber - 7 ? WeekStart.AddDays(7) : null;
    public TenantScheduleState State { get; init; }
    public bool IsDemo { get; init; }
    public IReadOnlyList<TenantViewingSlot> Slots { get; init; } = [];
    public static string DateLabel(DateOnly date) => date.ToString("dddd, dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
    public static string IsoDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
