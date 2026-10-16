using System.Text.Json;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Tenant.Services;

public static class RentalPresentation
{
    public const string Placeholder = "/images/room-placeholder.svg";

    public static string RoomTypeName(RoomType type) => type switch
    {
        RoomType.Room => "Phòng trọ", RoomType.Apartment => "Căn hộ",
        RoomType.House => "Nhà nguyên căn", RoomType.Dormitory => "Ký túc xá", _ => "Phòng cho thuê"
    };

    public static string? SafeMediaUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Any(char.IsControl) || value.Contains('\\')) return null;
        if (value.StartsWith('/') && !value.StartsWith("//")) return value;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == "https" || uri.Scheme == "http") && string.IsNullOrEmpty(uri.UserInfo) ? value : null;
    }

    public static List<string> Amenities(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 16384) return [];
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 8 });
            var root = document.RootElement;
            IEnumerable<string?> names = root.ValueKind switch
            {
                JsonValueKind.Array => root.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).ToArray(),
                JsonValueKind.Object => root.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.True).Select(x => x.Name).ToArray(),
                _ => []
            };
            return names.Where(x => !string.IsNullOrWhiteSpace(x) && x.Length <= 80).Select(x => x!.Trim()).Distinct().Take(30).ToList();
        }
        catch (JsonException) { return []; }
    }
}
