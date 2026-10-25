using System.ComponentModel.DataAnnotations;
using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Helpers;

namespace EZ_Rent.Areas.Landlord.ViewModels.Support;

/// <summary>Yêu cầu hỗ trợ gửi quản trị viên (R18). Giới hạn giống phía Tenant: tiêu đề ≤ 100, nội dung ≤ 1000 ký tự.</summary>
public sealed class SupportInput
{
    [Required(ErrorMessage = "Chọn loại yêu cầu.")]
    [RegularExpression("^(listing|tenant|payment|account|other)$", ErrorMessage = "Chọn loại yêu cầu.")]
    [Display(Name = "Loại yêu cầu")]
    public string Category { get; set; } = "listing";

    [Display(Name = "Phòng liên quan (không bắt buộc)")]
    public int? RoomId { get; set; }

    [Required(ErrorMessage = "Nhập tiêu đề.")]
    [StringLength(100, MinimumLength = 5, ErrorMessage = "Tiêu đề dài 5–100 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string Subject { get; set; } = "";

    [Required(ErrorMessage = "Mô tả vấn đề.")]
    [StringLength(1000, MinimumLength = 10, ErrorMessage = "Nội dung dài 10–1000 ký tự.")]
    [Display(Name = "Nội dung")]
    public string Content { get; set; } = "";
}

public sealed class SupportReplyInput
{
    [Required(ErrorMessage = "Nhập nội dung.")]
    [StringLength(1000, ErrorMessage = "Nội dung tối đa 1000 ký tự.")]
    public string Content { get; set; } = "";
}

public sealed class SupportRow
{
    public int Id { get; init; }
    public string? RoomTitle { get; init; }
    public string FirstMessage { get; init; } = "";
    public DateTime CreatedAt { get; init; }
    public DateTime LastAt { get; init; }
    public bool LastFromMe { get; init; }
    public int Unread { get; init; }
}

public sealed class SupportListModel
{
    public PaginatedList<SupportRow> Items { get; init; } = EmptyPage.Of<SupportRow>();
    public PaginationInfo Pager { get; init; } = new();
}

public sealed class SupportFormModel
{
    public SupportInput Input { get; init; } = new();
    public IReadOnlyList<OptionItem> Rooms { get; init; } = [];
    public bool AdministratorAvailable { get; init; } = true;
}

public sealed class SupportDetailModel
{
    public SupportRow Item { get; init; } = new();
    public string AdminName { get; init; } = "";
    public IReadOnlyList<SupportMessageRow> Messages { get; init; } = [];
}

public sealed record SupportMessageRow(int Id, string Content, bool Mine, DateTime SentAt);
