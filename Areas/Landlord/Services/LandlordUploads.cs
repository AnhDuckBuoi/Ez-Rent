using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace EZ_Rent.Areas.Landlord.Services;

public sealed record CheckedUpload(IFormFile File, string Extension, MediaType Type);

/// <summary>
/// Lưu ảnh/video do chủ trọ tải lên vào wwwroot/uploads/landlord.
/// An toàn: không tin phần mở rộng hay Content-Type của trình duyệt mà đọc chữ ký đầu file (magic bytes);
/// chỉ nhận JPEG/PNG/WebP (và MP4/WebM khi cho phép video); tên file do server sinh (GUID);
/// thư mục chỉ ghép từ số nguyên; không nhận SVG/HTML nên file tải lên không chạy được script.
/// </summary>
public static class LandlordUploads
{
    public const long MaxImageBytes = 5 * 1024 * 1024;
    public const long MaxVideoBytes = 25 * 1024 * 1024;
    public const string Root = "/uploads/landlord/";

    public static async Task<(CheckedUpload? Upload, string? Error)> CheckAsync(IFormFile? file, bool allowVideo, CancellationToken ct)
    {
        if (file == null || file.Length == 0) return (null, "File rỗng hoặc chưa được chọn.");
        var name = Path.GetFileName(file.FileName ?? "");
        var header = new byte[16];
        await using (var stream = file.OpenReadStream())
        {
            var read = 0;
            while (read < header.Length)
            {
                var n = await stream.ReadAsync(header.AsMemory(read, header.Length - read), ct);
                if (n == 0) break;
                read += n;
            }
            if (read < 12) return (null, $"“{name}” không phải ảnh/video hợp lệ.");
        }
        var detected = Detect(header);
        if (detected == null) return (null, $"“{name}” không đúng định dạng JPG, PNG, WebP{(allowVideo ? ", MP4 hoặc WebM" : "")}.");
        var (extension, type) = detected.Value;
        if (type == MediaType.Video && !allowVideo) return (null, $"“{name}”: chỉ nhận ảnh JPG, PNG hoặc WebP.");
        var limit = type == MediaType.Video ? MaxVideoBytes : MaxImageBytes;
        if (file.Length > limit) return (null, $"“{name}” vượt quá {(limit / 1024 / 1024)} MB.");
        return (new CheckedUpload(file, extension, type), null);
    }

    private static (string, MediaType)? Detect(byte[] h)
    {
        if (h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF) return (".jpg", MediaType.Image);
        if (h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47 && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A)
            return (".png", MediaType.Image);
        if (h[0] == 'R' && h[1] == 'I' && h[2] == 'F' && h[3] == 'F' && h[8] == 'W' && h[9] == 'E' && h[10] == 'B' && h[11] == 'P')
            return (".webp", MediaType.Image);
        if (h[4] == 'f' && h[5] == 't' && h[6] == 'y' && h[7] == 'p') return (".mp4", MediaType.Video);
        if (h[0] == 0x1A && h[1] == 0x45 && h[2] == 0xDF && h[3] == 0xA3) return (".webm", MediaType.Video);
        return null;
    }

    /// <summary>Lưu file đã kiểm tra, trả về URL tương đối (/uploads/landlord/...).</summary>
    public static async Task<string> SaveAsync(IWebHostEnvironment env, CheckedUpload upload, string kind, int ownerScopeId, CancellationToken ct)
    {
        if (kind is not ("rooms" or "meters")) throw new ArgumentOutOfRangeException(nameof(kind));
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRoot, "uploads", "landlord", kind, ownerScopeId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Directory.CreateDirectory(folder);
        var fileName = Guid.NewGuid().ToString("N") + upload.Extension;
        var path = Path.Combine(folder, fileName);
        await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var source = upload.File.OpenReadStream())
        {
            await source.CopyToAsync(target, ct);
        }
        return $"{Root}{kind}/{ownerScopeId}/{fileName}";
    }

    public const long MaxAvatarBytes = 2 * 1024 * 1024;

    /// <summary>Lưu ảnh đại diện chủ trọ vào /uploads/landlord/avatars/ (chỉ ảnh, tối đa 2 MB, đã kiểm tra bằng CheckAsync).</summary>
    public static async Task<string> SaveAvatarAsync(IWebHostEnvironment env, CheckedUpload upload, CancellationToken ct)
    {
        if (upload.Type != MediaType.Image || upload.File.Length > MaxAvatarBytes) throw new InvalidOperationException("Ảnh đại diện không hợp lệ.");
        var webRoot = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        var folder = Path.Combine(webRoot, "uploads", "landlord", "avatars");
        Directory.CreateDirectory(folder);
        var fileName = Guid.NewGuid().ToString("N") + upload.Extension;
        await using (var target = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew, FileAccess.Write, FileShare.None))
        await using (var source = upload.File.OpenReadStream())
        {
            await source.CopyToAsync(target, ct);
        }
        return $"{Root}avatars/{fileName}";
    }

    /// <summary>Chỉ xóa file nằm trong thư mục uploads/landlord; bỏ qua mọi đường dẫn khác.</summary>
    public static void TryDelete(IWebHostEnvironment env, string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || !url.StartsWith(Root, StringComparison.Ordinal) || url.Contains("..")) return;
        try
        {
            var webRoot = Path.GetFullPath(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"));
            var allowed = Path.GetFullPath(Path.Combine(webRoot, "uploads", "landlord")) + Path.DirectorySeparatorChar;
            var full = Path.GetFullPath(Path.Combine(webRoot, url.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));
            if (full.StartsWith(allowed, StringComparison.Ordinal) && File.Exists(full)) File.Delete(full);
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
