using EZ_Rent.Data;
using EZ_Rent.Models;

namespace EZ_Rent.Areas.Landlord.Services;

/// <summary>
/// Tạo thông báo cho người thuê. Link phải đúng mẫu mà Tenant chấp nhận
/// (TenantNotificationTarget: /Tenant/{ViewingAppointment|RentalPost|Contract|Invoice|RepairRequest}/Details/{id}),
/// nếu không người thuê vẫn đọc được thông báo nhưng không có nút mở nội dung.
/// Chỉ thêm vào DbContext; lời gọi SaveChanges thuộc về action đang xử lý để lưu cùng thay đổi chính.
/// </summary>
public static class LandlordNotifier
{
    public static string ViewingLink(int id) => $"/Tenant/ViewingAppointment/Details/{id}";
    public static string ContractLink(int id) => $"/Tenant/Contract/Details/{id}";
    public static string InvoiceLink(int id) => $"/Tenant/Invoice/Details/{id}";
    public static string RepairLink(int id) => $"/Tenant/RepairRequest/Details/{id}";

    public static void Add(AppDbContext db, string tenantId, string title, string content, string? link, string type, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(tenantId)) return;
        db.Notifications.Add(new Notification
        {
            UserId = tenantId,
            Title = LandlordFormat.Clean(title, 200) ?? "Thông báo",
            Content = LandlordFormat.Clean(content, 1000) ?? "",
            Link = link,
            Type = type,
            IsRead = false,
            CreatedAt = utcNow
        });
    }
}
