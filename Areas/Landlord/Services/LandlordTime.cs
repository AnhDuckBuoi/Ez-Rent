using System.Globalization;

namespace EZ_Rent.Areas.Landlord.Services;

/// <summary>
/// Quy ước thời gian dùng chung với phía Tenant:
/// - CreatedAt/UpdatedAt/PaidAt... lưu UTC, hiển thị giờ Việt Nam (UTC+7).
/// - ViewingAppointment.ViewingDate là giờ Việt Nam do Tenant chọn (datetime-local), không cộng/trừ múi giờ.
/// - Kỳ điện nước/hóa đơn có dạng "yyyy-MM".
/// </summary>
public static class LandlordTime
{
    public static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static DateTime ToVietnam(DateTime utc) =>
        DateTime.SpecifyKind(DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(VietnamOffset), DateTimeKind.Unspecified);

    public static DateTime? ToVietnam(DateTime? utc) => utc.HasValue ? ToVietnam(utc.Value) : null;

    public static string Period(DateTime vietnamTime) => vietnamTime.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static bool TryParsePeriod(string? value, out DateTime firstDay)
    {
        firstDay = default;
        return !string.IsNullOrWhiteSpace(value) && value.Length == 7 &&
            DateTime.TryParseExact(value + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out firstDay) &&
            firstDay.Year is >= 2000 and <= 2100;
    }

    public static string PeriodLabel(string? period) =>
        TryParsePeriod(period, out var day) ? $"Tháng {day:MM/yyyy}" : (period ?? "");

    public static string PreviousPeriod(string period) =>
        TryParsePeriod(period, out var day) ? day.AddMonths(-1).ToString("yyyy-MM", CultureInfo.InvariantCulture) : period;

    public static string Date(DateTime value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
    public static string Date(DateTime? value) => value.HasValue ? Date(value.Value) : "—";
    public static string Full(DateTime value) => value.ToString("dd/MM/yyyy · HH:mm", CultureInfo.InvariantCulture);
    public static string Time(DateTime value) => value.ToString("HH:mm", CultureInfo.InvariantCulture);
    public static string LongDate(DateTime value) => value.ToString("dddd, dd/MM/yyyy", Vi);

    /// <summary>Hiển thị một mốc UTC theo giờ Việt Nam.</summary>
    public static string Stamp(DateTime utc) => Full(ToVietnam(utc));
    public static string Stamp(DateTime? utc) => utc.HasValue ? Stamp(utc.Value) : "—";

    public static string Ago(DateTime utc, DateTime utcNow)
    {
        var span = utcNow - DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        if (span < TimeSpan.Zero) span = TimeSpan.Zero;
        if (span.TotalMinutes < 1) return "vừa xong";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} phút trước";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} giờ trước";
        return span.TotalDays < 30 ? $"{(int)span.TotalDays} ngày trước" : Stamp(utc);
    }
}
