using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Landlord.ViewModels;

public sealed class ViewingListInput
{
    [EnumDataType(typeof(ViewingAppointmentStatus))] public ViewingAppointmentStatus? Status { get; set; }
    [Range(1, int.MaxValue)] public int? Room { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
}

public sealed class ViewingRow
{
    public int Id { get; init; }
    public int RoomId { get; init; }
    public string RoomTitle { get; init; } = "";
    public string TenantName { get; init; } = "";
    public DateTime ViewingDate { get; init; }
    public ViewingAppointmentStatus Status { get; init; }
    public string? Note { get; init; }
    public string? LandlordNote { get; init; }
    public DateTime CreatedAt { get; init; }
    /// <summary>Giờ hẹn (giờ Việt Nam) đã qua.</summary>
    public bool IsPast { get; init; }
    /// <summary>Quá 2 giờ kể từ lúc gửi mà chưa phản hồi (quy tắc đã chốt phía Tenant, chưa có trạng thái "hết hạn" riêng).</summary>
    public bool ResponseLate { get; init; }
}

public sealed class ViewingListModel
{
    public ViewingListInput Input { get; init; } = new();
    public PagedList<ViewingRow> Items { get; init; } = new();
    public Dictionary<ViewingAppointmentStatus, int> Counts { get; init; } = new();
    public IReadOnlyList<(int Id, string Title)> Rooms { get; init; } = [];
    public bool InvalidQuery { get; init; }
}

public sealed class ViewingDetailModel
{
    public ViewingRow Item { get; init; } = new();
    public string TenantId { get; init; } = "";
    public string? TenantPhone { get; init; }
    public string? TenantEmail { get; init; }
    public string RoomAddress { get; init; } = "";
    public DateTime? UpdatedAt { get; init; }
    /// <summary>Các lịch khác của bạn trùng đúng giờ đến này (cùng người dẫn là chủ trọ).</summary>
    public IReadOnlyList<ViewingRow> SameTime { get; init; } = [];
}

public sealed class ViewingActionInput
{
    [StringLength(500, ErrorMessage = "Ghi chú tối đa 500 ký tự.")]
    public string? Note { get; set; }
}
