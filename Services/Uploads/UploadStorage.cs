using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace EZ_Rent.Services.Uploads;

public sealed record CheckedUpload(byte[] Bytes, string Extension, MediaType Type, string OriginalName);

/// <summary>
/// Ảnh/video CÔNG KHAI của tin phòng (người thuê cần xem trên trang tìm phòng) và ảnh đại diện.
/// Dùng chung quy tắc ảnh của nhóm: ImageHeaderValidation (PNG/JPEG, kiểm tra header + kích thước ≤ 4096 px).
/// Video chỉ nhận MP4/WebM theo chữ ký đầu file. Tên file do server sinh (GUID), không nhận SVG/HTML.
/// File riêng tư (ảnh công tơ, tài liệu hợp đồng) KHÔNG đi qua lớp này mà dùng RentalPrivateFileStore (ngoài wwwroot).
/// </summary>
public static class UploadStorage
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const long MaxVideoBytes = 25 * 1024 * 1024;
    public const long MaxAvatarBytes = 2 * 1024 * 1024;
    public const string RoomRoot = "/uploads/landlord/rooms/";
    public const string AvatarRoot = "/uploads/avatars/";

    public static async Task<(CheckedUpload? Upload, string? Error)> CheckAsync(IFormFile? file, bool allowVideo, long? imageLimit, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return (null, "File rỗng hoặc chưa được chọn.");
        var name = Path.GetFileName(file.FileName ?? "file");
        var limit = allowVideo ? MaxVideoBytes : (imageLimit ?? MaxImageBytes);
        if (file.Length > limit) return (null, $"“{name}” vượt quá {limit / 1024 / 1024} MB.");
        byte[] bytes;
        await using (var source = file.OpenReadStream())
        using (var buffer = new MemoryStream())
        {
            await source.CopyToAsync(buffer, ct);
            bytes = buffer.ToArray();
        }
        if (allowVideo && IsVideo(bytes, out var videoExt)) return (new CheckedUpload(bytes, videoExt, MediaType.Video, name), null);
        if (bytes.LongLength > (imageLimit ?? MaxImageBytes)) return (null, $"Ảnh “{name}” vượt quá {(imageLimit ?? MaxImageBytes) / 1024 / 1024} MB.");
        try
        {
            var ext = ImageHeaderValidation.Validate(bytes);
            return (new CheckedUpload(bytes, ext, MediaType.Image, name), null);
        }
        catch (InvalidDataException ex)
        {
            return (null, $"“{name}”: {ex.Message}" + (allowVideo ? " Video nhận MP4 hoặc WebM." : ""));
        }
    }

    private static bool IsVideo(byte[] h, out string extension)
    {
        extension = "";
        if (h.Length < 12) return false;
        if (h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p') { extension = ".mp4"; return true; }
        if (h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3) { extension = ".webm"; return true; }
        return false;
    }

    /// <summary>Ảnh/video phòng: /uploads/landlord/rooms/{postId}/{guid}.ext (thư mục uploads đã được .gitignore bỏ qua).</summary>
    public static Task<string> SaveRoomMediaAsync(IWebHostEnvironment env, CheckedUpload upload, int postId, CancellationToken ct) =>
        WriteAsync(env, upload, Path.Combine("uploads", "landlord", "rooms", postId.ToString(System.Globalization.CultureInfo.InvariantCulture)),
            $"{RoomRoot}{postId}/", ct);

    /// <summary>Ảnh đại diện: cùng thư mục /uploads/avatars/ với phía Tenant.</summary>
    public static Task<string> SaveAvatarAsync(IWebHostEnvironment env, CheckedUpload upload, CancellationToken ct) =>
        upload.Type == MediaType.Image && upload.Bytes.LongLength <= MaxAvatarBytes
            ? WriteAsync(env, upload, Path.Combine("uploads", "avatars"), AvatarRoot, ct)
            : throw new InvalidOperationException("Ảnh đại diện không hợp lệ.");

    private static async Task<string> WriteAsync(IWebHostEnvironment env, CheckedUpload upload, string relativeFolder, string urlPrefix, CancellationToken ct)
    {
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRoot, relativeFolder);
        Directory.CreateDirectory(folder);
        var fileName = Guid.NewGuid().ToString("N") + upload.Extension;
        await File.WriteAllBytesAsync(Path.Combine(folder, fileName), upload.Bytes, ct);
        return urlPrefix + fileName;
    }

    /// <summary>Chỉ xóa file nằm trong thư mục ảnh phòng hoặc ảnh đại diện; bỏ qua mọi đường dẫn khác.</summary>
    public static void TryDelete(IWebHostEnvironment env, string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Contains("..") ||
            !(url.StartsWith(RoomRoot, StringComparison.Ordinal) || url.StartsWith(AvatarRoot, StringComparison.Ordinal))) return;
        try
        {
            var webRoot = Path.GetFullPath(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
            var allowed = Path.GetFullPath(Path.Combine(webRoot, "uploads")) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(webRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            if (full.StartsWith(allowed, StringComparison.Ordinal) && File.Exists(full)) File.Delete(full);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
