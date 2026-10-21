using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Areas.Landlord.ViewModels;

public sealed class SupportInput
{
    [Required(ErrorMessage = "Chọn loại yêu cầu.")]
    [RegularExpression("^(help|feedback|report|other)$", ErrorMessage = "Chọn loại yêu cầu.")]
    [Display(Name = "Loại yêu cầu")]
    public string Category { get; set; } = "help";

    [Display(Name = "Phòng liên quan (không bắt buộc)")]
    public int? RoomId { get; set; }

    [Required(ErrorMessage = "Nhập tiêu đề.")]
    [StringLength(120, MinimumLength = 5, ErrorMessage = "Tiêu đề dài 5–120 ký tự.")]
    [Display(Name = "Tiêu đề")]
    public string Subject { get; set; } = "";

    [Required(ErrorMessage = "Mô tả vấn đề.")]
    [StringLength(1800, MinimumLength = 10, ErrorMessage = "Nội dung dài 10–1800 ký tự.")]
    [Display(Name = "Nội dung")]
    public string Content { get; set; } = "";

    public static string CategoryLabel(string? c) => c switch
    {
        "help" => "Hỗ trợ sử dụng", "feedback" => "Phản ánh", "report" => "Báo cáo vi phạm", _ => "Khác"
    };
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
    public int Count { get; init; }
}

public sealed class SupportListModel
{
    public PagedList<SupportRow> Items { get; init; } = new();
}

public sealed class SupportDetailModel
{
    public SupportRow Item { get; init; } = new();
    public string AdminName { get; init; } = "";
    public IReadOnlyList<MessageRow> Messages { get; init; } = [];
}
