using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using EZ_Rent.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Services;

/// <summary>
/// Khung giờ nhận khách xem phòng do chủ trọ mở (quyết định ngày 15/10 của nhóm: Tenant chọn giờ đến trong các giờ chủ nhà đã mở).
/// - Lịch lặp theo tuần: mỗi thứ một danh sách giờ đến (HH:mm).
/// - Ngoại lệ theo ngày: nghỉ cả ngày, hoặc dùng danh sách giờ riêng cho ngày đó.
/// Áp dụng cho mọi phòng của chủ trọ vì người dẫn xem là chính chủ trọ.
/// Không thêm bảng/migration: cấu hình lưu thành một claim JSON trong AspNetUserClaims (giống tài khoản nhận tiền).
/// </summary>
public sealed class ViewingHours
{
    public const int MaxTimesPerDay = 16;
    public const int MaxExceptions = 120;
    private static readonly char[] Separators = [',', ';', ' ', '\n', '\r', '\t'];

    /// <summary>Khóa 0..6 theo DayOfWeek (0 = Chủ nhật).</summary>
    [JsonPropertyName("weekly")] public Dictionary<int, List<string>> Weekly { get; set; } = new();

    /// <summary>Khóa yyyy-MM-dd. Danh sách rỗng = nghỉ cả ngày.</summary>
    [JsonPropertyName("dates")] public Dictionary<string, List<string>> Dates { get; set; } = new();

    public IReadOnlyList<TimeOnly> TimesFor(DateOnly date)
    {
        var key = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var source = Dates.TryGetValue(key, out var custom) ? custom : Weekly.GetValueOrDefault((int)date.DayOfWeek) ?? [];
        return source.Select(t => TryParseTime(t, out var v) ? v : (TimeOnly?)null)
            .Where(t => t.HasValue).Select(t => t!.Value).Distinct().Order().ToList();
    }

    public bool IsException(DateOnly date) => Dates.ContainsKey(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    public bool HasAnyTime => Weekly.Values.Any(v => v.Count > 0) || Dates.Values.Any(v => v.Count > 0);

    public static bool TryParseTime(string? text, out TimeOnly time) =>
        TimeOnly.TryParseExact(text?.Trim(), ["H:mm", "HH:mm"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);

    /// <summary>Đọc chuỗi "08:00, 9:30 17:00" → danh sách giờ chuẩn HH:mm. Trả lỗi nếu có giá trị sai.</summary>
    public static (List<string> Times, string? Error) ParseList(string? text)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return (result, null);
        foreach (var part in text.Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseTime(part, out var t)) return (result, $"“{part}” không phải giờ hợp lệ (dạng 08:30).");
            if (t.Minute % 5 != 0) return (result, $"“{part}”: dùng mốc 5 phút (ví dụ 08:30, 08:35).");
            var normalized = t.ToString("HH:mm", CultureInfo.InvariantCulture);
            if (!result.Contains(normalized)) result.Add(normalized);
        }
        if (result.Count > MaxTimesPerDay) return (result, $"Tối đa {MaxTimesPerDay} giờ đến mỗi ngày.");
        result.Sort(StringComparer.Ordinal);
        return (result, null);
    }

    /// <summary>Bỏ ngoại lệ của những ngày đã qua để dữ liệu không phình ra.</summary>
    public void PruneBefore(DateOnly today)
    {
        foreach (var key in Dates.Keys.ToList())
            if (!DateOnly.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) || d < today)
                Dates.Remove(key);
    }
}

public static class LandlordViewingHours
{
    public const string Claim = "ezrent:landlord:viewing-hours";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false };

    public static async Task<ViewingHours?> ReadAsync(AppDbContext db, string landlordId, CancellationToken ct = default)
    {
        var value = await db.UserClaims.AsNoTracking()
            .Where(c => c.UserId == landlordId && c.ClaimType == Claim).Select(c => c.ClaimValue).FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(value) || value.Length > 64_000) return null;
        try { return JsonSerializer.Deserialize<ViewingHours>(value, Json); }
        catch (JsonException) { return null; }
    }

    public static async Task SaveAsync(AppDbContext db, string landlordId, ViewingHours hours, CancellationToken ct = default)
    {
        var old = await db.UserClaims.Where(c => c.UserId == landlordId && c.ClaimType == Claim).ToListAsync(ct);
        db.UserClaims.RemoveRange(old);
        db.UserClaims.Add(new IdentityUserClaim<string> { UserId = landlordId, ClaimType = Claim, ClaimValue = JsonSerializer.Serialize(hours, Json) });
        await db.SaveChangesAsync(ct);
    }
}
