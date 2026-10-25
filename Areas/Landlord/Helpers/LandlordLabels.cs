using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Landlord.Helpers;

/// <summary>Một nơi duy nhất cho nhãn trạng thái, theo đúng tên trong báo cáo (R15, R42, BM05). Tone là màu badge Bootstrap.</summary>
public readonly record struct StatusLabel(string Text, string Tone);

public static class LandlordLabels
{
    public static StatusLabel Approval(ApprovalStatus s) => s switch
    {
        ApprovalStatus.Draft => new("Bản nháp", "secondary"),
        ApprovalStatus.Pending => new("Chờ duyệt", "primary"),
        ApprovalStatus.Approved => new("Đã duyệt", "success"),
        ApprovalStatus.Rejected => new("Bị từ chối", "danger"),
        ApprovalStatus.NeedRevision => new("Cần chỉnh sửa", "warning"),
        _ => new("Chưa xác định", "secondary")
    };

    public static StatusLabel Room(RoomStatus s) => s switch
    {
        RoomStatus.Available => new("Còn trống", "success"),
        RoomStatus.Rented => new("Đã cho thuê", "info"),
        RoomStatus.Hidden => new("Đang ẩn", "secondary"),
        _ => new("Chưa xác định", "secondary")
    };

    public static StatusLabel Viewing(ViewingAppointmentStatus s) => s switch
    {
        ViewingAppointmentStatus.Pending => new("Chờ xác nhận", "primary"),
        ViewingAppointmentStatus.Confirmed => new("Đã xác nhận", "success"),
        ViewingAppointmentStatus.Completed => new("Đã hoàn thành", "info"),
        ViewingAppointmentStatus.Cancelled => new("Đã hủy", "secondary"),
        ViewingAppointmentStatus.Rejected => new("Bị từ chối", "danger"),
        _ => new("Chưa xác định", "secondary")
    };

    public static StatusLabel Contract(ContractStatus s) => s switch
    {
        ContractStatus.Draft => new("Bản nháp", "secondary"),
        ContractStatus.PendingTenant => new("Chờ người thuê xác nhận", "primary"),
        ContractStatus.PendingLandlord => new("Chờ bạn xác nhận", "warning"),
        ContractStatus.Active => new("Đang hiệu lực", "success"),
        ContractStatus.Expired => new("Đã kết thúc", "info"),
        ContractStatus.Cancelled => new("Đã hủy", "secondary"),
        _ => new("Chưa xác định", "secondary")
    };

    public static StatusLabel Payment(PaymentStatus s, bool overdue = false) => s switch
    {
        PaymentStatus.Unpaid when overdue => new("Quá hạn", "danger"),
        PaymentStatus.Unpaid => new("Chưa thanh toán", "primary"),
        PaymentStatus.AwaitingConfirmation => new("Chờ xác nhận tiền", "warning"),
        PaymentStatus.Paid => new("Đã thanh toán", "success"),
        PaymentStatus.Overdue => new("Quá hạn", "danger"),
        PaymentStatus.Cancelled => new("Đã hủy", "secondary"),
        _ => new("Chưa xác định", "secondary")
    };

    public static StatusLabel Repair(RepairRequestStatus s) => s switch
    {
        RepairRequestStatus.Pending => new("Chờ xử lý", "primary"),
        RepairRequestStatus.InProgress => new("Đang xử lý", "warning"),
        RepairRequestStatus.Resolved => new("Đã xử lý", "success"),
        RepairRequestStatus.Cancelled => new("Đã hủy", "secondary"),
        _ => new("Chưa xác định", "secondary")
    };

    public static StatusLabel Priority(RepairPriority p) => p switch
    {
        RepairPriority.Low => new("Thấp", "secondary"),
        RepairPriority.Medium => new("Trung bình", "info"),
        RepairPriority.High => new("Cao", "warning"),
        RepairPriority.Urgent => new("Khẩn cấp", "danger"),
        _ => new("Không rõ", "secondary")
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
