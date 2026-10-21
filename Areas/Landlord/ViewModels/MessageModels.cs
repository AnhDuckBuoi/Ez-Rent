using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Areas.Landlord.ViewModels;

public sealed class MessagesInput
{
    [Range(1, int.MaxValue)] public int? C { get; set; }
    [RegularExpression("^(all|unread)$")] public string? Filter { get; set; }
    [StringLength(100)] public string? Q { get; set; }
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, int.MaxValue)] public int MessagePage { get; set; } = 1;
}

public sealed class ConversationRow
{
    public int Id { get; init; }
    public string OtherId { get; init; } = "";
    public string OtherName { get; init; } = "";
    public int? RoomId { get; init; }
    public string? RoomTitle { get; init; }
    public DateTime LastAt { get; init; }
    public string? LastContent { get; init; }
    public bool LastFromMe { get; init; }
    public int Unread { get; init; }
}

public sealed record MessageRow(int Id, string Content, bool Mine, DateTime SentAt, bool IsRead);

public sealed class MessagesModel
{
    public MessagesInput Input { get; init; } = new();
    public PagedList<ConversationRow> Conversations { get; init; } = new();
    public ConversationRow? Selected { get; init; }
    public PagedList<MessageRow> Messages { get; init; } = new();
    public string? OtherPhone { get; init; }
    public int TotalUnread { get; init; }
    public bool Unavailable { get; init; }
}

public sealed class SendMessageInput
{
    [Required(ErrorMessage = "Nhập nội dung tin nhắn.")]
    [StringLength(2000, ErrorMessage = "Tin nhắn tối đa 2000 ký tự.")]
    public string Content { get; set; } = "";
}
