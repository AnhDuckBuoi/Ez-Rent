using Microsoft.AspNetCore.Http;

namespace EZ_Rent.Services.Meters;

/// <summary>
/// Ảnh công tơ điện/nước lưu RIÊNG TƯ (ngoài wwwroot) bằng RentalPrivateFileStore của nhóm, cùng kho với tài liệu hợp đồng.
/// MeterReading.ElectricMeterImageUrl/WaterMeterImageUrl chỉ giữ tham chiếu "rental-file:{key}" (không phải URL công khai):
/// - chủ trọ xem qua MeterPhotosController (kiểm tra quyền sở hữu phòng);
/// - người thuê xem qua InvoiceMeterEvidence: khi phát hành hóa đơn, ảnh được chuyển cho
///   RentalDocumentService.CaptureInvoiceAsync, dịch vụ này tự kiểm tra rồi sao thành bản riêng gắn với hóa đơn.
/// Trang Tenant không đọc hai trường URL cũ nên tham chiếu này không hiện ra phía người thuê.
/// </summary>
public static class MeterPhotoFiles
{
    public const string Prefix = "rental-file:";

    public static async Task<string> SaveAsync(RentalPrivateFileStore store, IFormFile file, CancellationToken ct) =>
        Prefix + (await store.SaveAsync(file, allowPdf: false, ct)).Key;

    public static string? KeyOf(string? reference) =>
        reference != null && reference.StartsWith(Prefix, StringComparison.Ordinal) ? reference[Prefix.Length..] : null;

    public static bool HasPhoto(string? reference) => KeyOf(reference) != null;

    public static string ContentTypeOf(string key) => key.EndsWith(".png", StringComparison.Ordinal) ? "image/png" : "image/jpeg";

    public static void Delete(RentalPrivateFileStore store, string? reference)
    {
        var key = KeyOf(reference);
        if (key != null) store.DeleteUncommitted(key);
    }

    /// <summary>Dựng IFormFile từ ảnh đã lưu để chuyển cho CaptureInvoiceAsync (dịch vụ yêu cầu IFormFile).</summary>
    public static async Task<IFormFile?> AsFormFileAsync(RentalPrivateFileStore store, string? reference, string name, CancellationToken ct)
    {
        var key = KeyOf(reference);
        if (key == null) return null;
        await using var source = store.OpenRead(key);
        if (source == null) return null;
        var buffer = new MemoryStream();
        await source.CopyToAsync(buffer, ct);
        buffer.Position = 0;
        return new FormFile(buffer, 0, buffer.Length, name, name + Path.GetExtension(key))
        {
            Headers = new HeaderDictionary(),
            ContentType = ContentTypeOf(key)
        };
    }
}
