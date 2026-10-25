using System.ComponentModel.DataAnnotations;
using System.Globalization;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed class AppointmentListInput
{
    [EnumDataType(typeof(ViewingAppointmentStatus), ErrorMessage = "Trạng thái lịch hẹn không hợp lệ.")]
    public ViewingAppointmentStatus? Status { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Số trang phải là số nguyên dương.")]
    public int Page { get; set; } = 1;
}

public sealed class AppointmentListModel
{
    public AppointmentListInput Input { get; init; } = new();
    public IReadOnlyList<AppointmentModel> Items { get; init; } = [];
    public Dictionary<ViewingAppointmentStatus, int> Counts { get; init; } = new();
    public int Total => Counts.Values.Sum();
    public int FilteredTotal { get; init; }
    public int TotalPages { get; init; } = 1;
}

public sealed class AppointmentModel
{
    public int Id { get; init; }
    public int PostId { get; init; }
    public string Title { get; init; } = "";
    public string Address { get; init; } = "";
    public bool PostAvailable { get; init; }
    public DateTime ViewingDate { get; init; }
    public ViewingAppointmentStatus Status { get; init; }
    public string? Note { get; init; }
    public string? LandlordNote { get; init; }
    public string DateLabel => ViewingDate.ToString("dddd, dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
    public string TimeLabel => ViewingDate.ToString("HH:mm", CultureInfo.InvariantCulture);
    // Keep the existing cancellation rule. A time cutoff needs a shared time-storage contract first.
    public bool CanCancel => Status is ViewingAppointmentStatus.Pending or ViewingAppointmentStatus.Confirmed;
    public string StatusLabel => Label(Status);
    public string StatusClass => Status switch
    {
        ViewingAppointmentStatus.Pending => "pending",
        ViewingAppointmentStatus.Confirmed => "confirmed",
        ViewingAppointmentStatus.Completed => "completed",
        ViewingAppointmentStatus.Cancelled => "cancelled",
        ViewingAppointmentStatus.Rejected => "rejected",
        _ => "cancelled"
    };
    public string Explanation => Status switch
    {
        ViewingAppointmentStatus.Pending => "Yêu cầu đã được gửi. Hãy đợi chủ nhà xác nhận trước khi đến xem phòng.",
        ViewingAppointmentStatus.Confirmed => "Chủ nhà đã xác nhận lịch xem phòng của bạn.",
        ViewingAppointmentStatus.Completed => "Lịch xem phòng đã hoàn tất. Bạn có thể xem lại thông tin tại đây.",
        ViewingAppointmentStatus.Cancelled => "Lịch hẹn đã được hủy. Nội dung trao đổi được giữ lại để bạn tra cứu.",
        ViewingAppointmentStatus.Rejected => "Chủ nhà đã từ chối yêu cầu này. Xem phản hồi bên dưới để biết thêm thông tin.",
        _ => "Trạng thái lịch hẹn chưa xác định. Vui lòng tải lại danh sách."
    };
    public static string Label(ViewingAppointmentStatus status) => status switch
    {
        ViewingAppointmentStatus.Pending => "Chờ xác nhận",
        ViewingAppointmentStatus.Confirmed => "Đã xác nhận",
        ViewingAppointmentStatus.Completed => "Đã hoàn tất",
        ViewingAppointmentStatus.Cancelled => "Đã hủy",
        ViewingAppointmentStatus.Rejected => "Bị từ chối",
        _ => "Chưa xác định"
    };

    public static AppointmentModel From(ViewingAppointment appointment)
    {
        var post = appointment.RentalPost;
        var available = post.ApprovalStatus == ApprovalStatus.Approved && post.RoomStatus == RoomStatus.Available;
        return new()
        {
            Id = appointment.ViewingAppointmentId, PostId = appointment.RentalPostId,
            PostAvailable = available, Title = available ? post.Title : "Tin phòng không còn khả dụng",
            Address = available ? post.Address : "Tin phòng hiện không được công khai hoặc không còn trống.",
            ViewingDate = appointment.ViewingDate, Status = appointment.Status,
            Note = appointment.Note, LandlordNote = appointment.LandlordNote
        };
    }
}
