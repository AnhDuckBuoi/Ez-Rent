using System.Globalization;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Tenant.Services;

public static class TenantRentalPresentation
{
    public const int PageSize = 10;
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");
    public static int Pages(int count) => Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
    public static string Money(decimal value) => value.ToString("N0", Vietnamese) + " đ";
    public static string Number(double value) => value.ToString("N2", Vietnamese);
    public static string Date(DateTime value) => value.ToString("dd/MM/yyyy", Vietnamese);
    // CreatedAt/UpdatedAt are stored in UTC by the existing models.
    public static string SentAt(DateTime value) => value.AddHours(7).ToString("dd/MM/yyyy · HH:mm", Vietnamese);
    public static string Period(string value) => DateTime.TryParseExact(value, "yyyy-MM", CultureInfo.InvariantCulture,
        DateTimeStyles.None, out var date) ? date.ToString("MM/yyyy", Vietnamese) : value;

    public static bool CanChange(RepairRequest request) => request.Status == RepairRequestStatus.Pending;

    public static string ContractLabel(ContractStatus status) => status switch
    {
        ContractStatus.Draft => "Bản nháp",
        ContractStatus.PendingTenant => "Chờ người thuê",
        ContractStatus.PendingLandlord => "Chờ chủ nhà",
        ContractStatus.Active => "Đang hiệu lực",
        ContractStatus.Expired => "Đã hết hạn",
        ContractStatus.Cancelled => "Đã hủy",
        _ => "Chưa xác định"
    };
    public static string PaymentLabel(PaymentStatus status) => status switch
    {
        PaymentStatus.Unpaid => "Chưa thanh toán",
        PaymentStatus.AwaitingConfirmation => "Chờ đối soát",
        PaymentStatus.Paid => "Đã thanh toán",
        PaymentStatus.Overdue => "Quá hạn",
        PaymentStatus.Cancelled => "Đã hủy",
        _ => "Chưa xác định"
    };
    public static string RepairLabel(RepairRequestStatus status) => status switch
    {
        RepairRequestStatus.Pending => "Chờ xử lý",
        RepairRequestStatus.InProgress => "Đang xử lý",
        RepairRequestStatus.Resolved => "Đã hoàn tất",
        RepairRequestStatus.Cancelled => "Đã hủy",
        _ => "Chưa xác định"
    };
    public static string PriorityLabel(RepairPriority priority) => priority switch
    {
        RepairPriority.Low => "Thấp",
        RepairPriority.Medium => "Bình thường",
        RepairPriority.High => "Cao",
        RepairPriority.Urgent => "Khẩn cấp",
        _ => "Chưa xác định"
    };
    public static string Tone(PaymentStatus status) => status switch
    {
        PaymentStatus.Paid => "success",
        PaymentStatus.Overdue => "danger",
        PaymentStatus.Unpaid or PaymentStatus.AwaitingConfirmation => "waiting",
        _ => "neutral"
    };
    public static string Tone(ContractStatus status) => status switch
    {
        ContractStatus.Active => "success",
        ContractStatus.PendingTenant or ContractStatus.PendingLandlord => "waiting",
        _ => "neutral"
    };
    public static string Tone(RepairRequestStatus status) => status switch
    {
        RepairRequestStatus.Resolved => "success",
        RepairRequestStatus.Pending => "waiting",
        RepairRequestStatus.InProgress => "progress",
        _ => "neutral"
    };
}
