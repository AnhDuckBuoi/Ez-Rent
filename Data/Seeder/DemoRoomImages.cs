using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Data.Seeder;

/// <summary>
/// Ảnh minh họa phòng trọ LƯU TRONG DỰ ÁN (wwwroot/images/rooms, vẽ bằng scripts/tao-anh-phong-minh-hoa.py),
/// thay cho ảnh ngẫu nhiên của picsum.photos (hay lạc chủ đề và cần mạng). Ảnh được chọn theo loại phòng:
/// ảnh đầu (ảnh đại diện) là không gian chính, các ảnh sau là nhà vệ sinh, bếp, ban công, chỗ để xe…
/// Muốn dùng ảnh thật của phòng, chỉ cần chủ trọ tải ảnh lên ở trang chi tiết tin.
/// </summary>
public static class DemoRoomImages
{
    public const string Root = "/images/rooms/";
    public const string RepairImage = Root + "su-co-dieu-hoa.jpg";

    private static string[] Set(string name, int count) => Enumerable.Range(1, count).Select(i => $"{Root}{name}-{i}.jpg").ToArray();
    private static readonly string[] Bedroom = Set("phong-ngu", 4), Loft = Set("studio-gac", 3), Living = Set("phong-khach", 3),
        Kitchen = Set("bep", 3), Bathroom = Set("nha-ve-sinh", 3), Dorm = Set("ky-tuc-xa", 3), Balcony = Set("ban-cong", 3),
        House = Set("nha-nguyen-can", 3), Parking = Set("cho-de-xe", 2);

    /// <summary>Ảnh thứ <paramref name="index"/> (0 = ảnh đại diện) cho một phòng; <paramref name="seed"/> chọn biến thể màu ổn định.</summary>
    public static string For(RoomType type, int seed, int index, bool loft = false)
    {
        string[][] sequence = type switch
        {
            RoomType.Apartment => [Living, Bedroom, Kitchen, Bathroom],
            RoomType.House => [House, Living, Bedroom, Kitchen],
            RoomType.Dormitory => [Dorm, Bathroom, Parking, Balcony],
            _ => loft ? [Loft, Bathroom, Balcony, Parking] : [Bedroom, Bathroom, Balcony, Parking]
        };
        var group = sequence[Math.Abs(index) % sequence.Length];
        return group[Math.Abs(seed) % group.Length];
    }

    /// <summary>
    /// Đổi các ảnh picsum.photos đã có trong database (do bản seed cũ tạo) sang ảnh trong dự án. Chạy một lần khi khởi động
    /// ở môi trường Development/Demo (LandlordDemoSeedRunner); không còn ảnh picsum thì không làm gì.
    /// </summary>
    public static async Task<int> ReplaceRemoteAsync(AppDbContext db, CancellationToken ct)
    {
        const string remote = "https://picsum.photos/";
        var images = await db.RoomImages.Include(i => i.RentalPost).Where(i => i.MediaUrl.StartsWith(remote)).ToListAsync(ct);
        foreach (var image in images)
            image.MediaUrl = For(image.RentalPost.RoomType, image.RentalPostId, image.DisplayOrder,
                image.RentalPost.Title.Contains("gác", StringComparison.OrdinalIgnoreCase));
        var repairs = await db.RepairRequests.Where(r => r.ImageUrl != null && r.ImageUrl.StartsWith(remote)).ToListAsync(ct);
        foreach (var repair in repairs) repair.ImageUrl = RepairImage;
        if (images.Count + repairs.Count > 0) await db.SaveChangesAsync(ct);
        return images.Count + repairs.Count;
    }
}
