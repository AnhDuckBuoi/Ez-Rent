using System.Globalization;
using System.Text;

namespace EZ_Rent.Services.Common;

public static class DisplayFormat
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");

    public static string Money(decimal value) => value.ToString("#,##0", Vi) + " đ";
    public static string Number(decimal value) => value.ToString("#,##0", Vi);
    public static string Number(double value) => value.ToString("#,##0.##", Vi);
    public static string Area(double value) => value.ToString("#,##0.#", Vi) + " m²";

    /// <summary>Đường dẫn ảnh/video lưu trong DB vẫn là dữ liệu không tin cậy khi đưa ra HTML.</summary>
    public static string? SafeMediaUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        if (value.Length > 500 || value.Any(char.IsControl) || value.Contains('\\')) return null;
        if (value.StartsWith('/') && !value.StartsWith("//", StringComparison.Ordinal)) return value;
        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp) && string.IsNullOrEmpty(uri.UserInfo) ? value : null;
    }

    public static string? Clean(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        value = value.Trim();
        return value.Length <= max ? value : value[..max];
    }

    /// <summary>Bỏ dấu tiếng Việt, chỉ giữ chữ/số/khoảng trắng (dùng cho tên tài khoản và nội dung VietQR).</summary>
    public static string Ascii(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var normalized = value.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (c < 128 && (char.IsLetterOrDigit(c) || c == ' ')) builder.Append(c);
        }
        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }
}
