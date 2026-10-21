using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Landlord.Services;

/// <summary>Một nơi duy nhất cho nhãn trạng thái, theo đúng tên trong báo cáo (R15, R42, BM05).</summary>
public readonly record struct StatusLabel(string Text, string Tone);

public static class LandlordLabels
{
    public static StatusLabel Approval(ApprovalStatus s) => s switch
    {
        ApprovalStatus.Draft => new("Bản nháp", "muted"),
        ApprovalStatus.Pending => new("Chờ duyệt", "pending"),
        ApprovalStatus.Approved => new("Đã duyệt", "ok"),
        ApprovalStatus.Rejected => new("Bị từ chối", "danger"),
        ApprovalStatus.NeedRevision => new("Cần chỉnh sửa", "warn"),
        _ => new("Chưa xác định", "muted")
    };

    public static StatusLabel Room(RoomStatus s) => s switch
    {
        RoomStatus.Available => new("Còn trống", "ok"),
        RoomStatus.Rented => new("Đã cho thuê", "info"),
        RoomStatus.Hidden => new("Đang ẩn", "muted"),
        _ => new("Chưa xác định", "muted")
    };

    public static StatusLabel Viewing(ViewingAppointmentStatus s) => s switch
    {
        ViewingAppointmentStatus.Pending => new("Chờ xác nhận", "pending"),
        ViewingAppointmentStatus.Confirmed => new("Đã xác nhận", "ok"),
        ViewingAppointmentStatus.Completed => new("Đã hoàn thành", "info"),
        ViewingAppointmentStatus.Cancelled => new("Đã hủy", "muted"),
        ViewingAppointmentStatus.Rejected => new("Bị từ chối", "danger"),
        _ => new("Chưa xác định", "muted")
    };

    public static StatusLabel Contract(ContractStatus s) => s switch
    {
        ContractStatus.Draft => new("Bản nháp", "muted"),
        ContractStatus.PendingTenant => new("Chờ người thuê xác nhận", "pending"),
        ContractStatus.PendingLandlord => new("Chờ bạn xác nhận", "warn"),
        ContractStatus.Active => new("Đang hiệu lực", "ok"),
        ContractStatus.Expired => new("Đã kết thúc", "info"),
        ContractStatus.Cancelled => new("Đã hủy", "muted"),
        _ => new("Chưa xác định", "muted")
    };

    public static StatusLabel Payment(PaymentStatus s, bool overdue = false) => s switch
    {
        PaymentStatus.Unpaid when overdue => new("Quá hạn", "danger"),
        PaymentStatus.Unpaid => new("Chưa thanh toán", "pending"),
        PaymentStatus.AwaitingConfirmation => new("Chờ xác nhận tiền", "warn"),
        PaymentStatus.Paid => new("Đã thanh toán", "ok"),
        PaymentStatus.Overdue => new("Quá hạn", "danger"),
        PaymentStatus.Cancelled => new("Đã hủy", "muted"),
        _ => new("Chưa xác định", "muted")
    };

    public static StatusLabel Repair(RepairRequestStatus s) => s switch
    {
        RepairRequestStatus.Pending => new("Chờ xử lý", "pending"),
        RepairRequestStatus.InProgress => new("Đang xử lý", "warn"),
        RepairRequestStatus.Resolved => new("Đã xử lý", "ok"),
        RepairRequestStatus.Cancelled => new("Đã hủy", "muted"),
        _ => new("Chưa xác định", "muted")
    };

    public static StatusLabel Priority(RepairPriority p) => p switch
    {
        RepairPriority.Low => new("Thấp", "muted"),
        RepairPriority.Medium => new("Trung bình", "info"),
        RepairPriority.High => new("Cao", "warn"),
        RepairPriority.Urgent => new("Khẩn cấp", "danger"),
        _ => new("Không rõ", "muted")
    };

    public static string RoomTypeName(RoomType t) => t switch
    {
        RoomType.Room => "Phòng trọ",
        RoomType.Apartment => "Căn hộ",
        RoomType.House => "Nhà nguyên căn",
        RoomType.Dormitory => "Ký túc xá",
        _ => "Phòng cho thuê"
    };

    public static readonly IReadOnlyList<string> Amenities =
    [
        "Wifi", "Điều hòa", "Nóng lạnh", "Máy giặt", "Tủ lạnh", "Giường", "Tủ quần áo", "Bếp riêng",
        "WC khép kín", "Ban công", "Chỗ để xe", "Thang máy", "Giờ giấc tự do", "Không chung chủ",
        "Camera an ninh", "Cho nuôi thú cưng"
    ];
}
