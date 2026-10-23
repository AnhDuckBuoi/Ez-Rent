using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services.Uploads;
using Microsoft.AspNetCore.Hosting;

namespace EZ_Rent.Services.Rooms;

/// <summary>
/// Ghi ảnh/video của tin phòng: lưu file công khai (UploadStorage) rồi thêm RoomImage vào DbContext.
/// Giữ quy tắc phía Tenant đang đọc: đúng một ảnh đại diện (IsPrimary, phải là ảnh) và thứ tự DisplayOrder.
/// Chỉ thêm vào DbContext; action gọi tới tự SaveChanges.
/// </summary>
public static class RoomMediaWriter
{
    public const int MaxMediaPerRoom = 12;

    public static async Task<int> AddAsync(AppDbContext db, IWebHostEnvironment env, int postId, IReadOnlyList<CheckedUpload> uploads,
        int startOrder, bool hasPrimary, DateTime utcNow, CancellationToken ct)
    {
        var saved = 0;
        foreach (var upload in uploads)
        {
            try
            {
                var url = await UploadStorage.SaveRoomMediaAsync(env, upload, postId, ct);
                var primary = !hasPrimary && upload.Type == MediaType.Image;
                if (primary) hasPrimary = true;
                db.RoomImages.Add(new RoomImage
                {
                    RentalPostId = postId, MediaUrl = url, MediaType = upload.Type,
                    IsPrimary = primary, DisplayOrder = startOrder + saved, CreatedAt = utcNow
                });
                saved++;
            }
            catch (IOException) { }
        }
        return saved;
    }
}
