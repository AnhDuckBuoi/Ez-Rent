namespace EZ_Rent.Areas.Landlord.Helpers;

/// <summary>Số đếm hiện cạnh một mục menu (lịch chờ, sự cố mới, phản hồi hỗ trợ).</summary>
public enum LandlordBadge { None, PendingViewings, OpenRepairs, SupportReplies }

/// <summary>
/// Một mục menu. Matches = các controller thuộc mục này để tô sáng đúng mục khi đang ở trang con
/// (ví dụ trang ảnh/video phòng vẫn sáng "Danh sách phòng"). Area rỗng = trang chung (Chat).
/// </summary>
public sealed record LandlordNavItem(string Label, string Controller, string Action, string[] Matches,
    LandlordBadge Badge = LandlordBadge.None, string Area = "Landlord", string Icon = "");

public sealed record LandlordNavGroup(string Key, string Icon, string Label, IReadOnlyList<LandlordNavItem> Items)
{
    public bool Contains(string? area, string? controller) => controller != null &&
        Items.Any(i => string.Equals(i.Area, area ?? "", StringComparison.OrdinalIgnoreCase) &&
                       i.Matches.Contains(controller, StringComparer.OrdinalIgnoreCase));

    /// <summary>Mục đang mở: ưu tiên mục trùng cả controller lẫn action (Đăng tin mới), sau đó tới mục chứa controller.</summary>
    public LandlordNavItem? ActiveItem(string? area, string? controller, string? action) =>
        Items.FirstOrDefault(i => i.Action != "Index" && string.Equals(i.Area, area ?? "", StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(i.Controller, controller, StringComparison.OrdinalIgnoreCase) &&
                                  string.Equals(i.Action, action, StringComparison.OrdinalIgnoreCase))
        ?? Items.FirstOrDefault(i => string.Equals(i.Area, area ?? "", StringComparison.OrdinalIgnoreCase) &&
                                     i.Matches.Contains(controller ?? "", StringComparer.OrdinalIgnoreCase));
}

/// <summary>
/// Một nơi duy nhất định nghĩa menu chủ trọ, chia theo nhóm chức năng liên quan:
/// - Menu trên thanh điều hướng chung (_LandlordHeaderMenu, _Layout gọi giống cách gọi _TenantSharedMenu của Tenant);
/// - Thanh chức năng trong trang (_LandlordMenu) chỉ hiện các mục cùng nhóm với trang đang mở.
/// Muốn đổi tên, thứ tự hay chuyển mục sang nhóm khác, chỉ sửa danh sách dưới đây.
/// </summary>
public static class LandlordNavigation
{
    public static readonly IReadOnlyList<LandlordNavGroup> Groups =
    [
        new("overview", "dashboard", "Tổng quan",
        [
            new("Bảng tổng quan", "Overview", "Index", ["Overview"], Icon: "chart"),
            new("Tin nhắn với người thuê", "Chat", "Index", ["Chat", "Messages"], Area: "", Icon: "chat"),
            new("Yêu cầu sửa chữa", "Repairs", "Index", ["Repairs"], LandlordBadge.OpenRepairs, Icon: "wrench"),
            new("Hỗ trợ từ quản trị viên", "Support", "Index", ["Support"], LandlordBadge.SupportReplies, Icon: "support")
        ]),
        new("rooms", "building", "Phòng trọ",
        [
            new("Danh sách phòng", "Rooms", "Index", ["Rooms", "RoomMedia"], Icon: "building"),
            new("Đăng tin mới", "Rooms", "Create", [], Icon: "plus"),
            new("Lịch xem phòng", "Viewings", "Index", ["Viewings"], LandlordBadge.PendingViewings, Icon: "calendar"),
            new("Khung giờ nhận khách", "ViewingHours", "Index", ["ViewingHours"], Icon: "clock")
        ]),
        new("rental", "contract", "Hợp đồng",
        [
            new("Hợp đồng", "Contracts", "Index", ["Contracts", "ContractDocuments"], Icon: "contract"),
            new("Điện, nước", "MeterReadings", "Index", ["MeterReadings", "MeterPhotos"], Icon: "bolt"),
            new("Hóa đơn", "Invoices", "Index", ["Invoices", "InvoicePayments"], Icon: "receipt"),
            new("Tài khoản nhận tiền", "PaymentSettings", "Index", ["PaymentSettings"], Icon: "bank")
        ]),
        new("account", "user", "Tài khoản",
        [
            new("Tài khoản chủ trọ", "Account", "Index", ["Account"], Icon: "user")
        ])
    ];

    public static LandlordNavGroup? GroupOf(string? area, string? controller) => Groups.FirstOrDefault(g => g.Contains(area, controller));
}
