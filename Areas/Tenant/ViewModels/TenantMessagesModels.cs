using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed class TenantMessagesInput
{
    [Range(1, int.MaxValue)] public int? ConversationId { get; set; }
    [Range(1, int.MaxValue)] public int? RentalPostId { get; set; }
    [StringLength(100, ErrorMessage = "Từ khóa tối đa 100 ký tự.")] public string? Query { get; set; }
    [RegularExpression("^(all|unread|support)$")] public string Filter { get; set; } = "all";
    [Range(1, int.MaxValue)] public int Page { get; set; } = 1;
    [Range(1, int.MaxValue)] public int MessagePage { get; set; } = 1;
    [RegularExpression("^(inbox|chat)$")] public string View { get; set; } = "chat";
    public object Routes(int? conversationId = null, int? page = null, int? messagePage = null,
        string? filter = null, string? view = null) => new
    {
        ConversationId = conversationId ?? ConversationId, RentalPostId = conversationId.HasValue ? null : RentalPostId, Query, Filter = filter ?? Filter,
        Page = page ?? Page, MessagePage = messagePage ?? MessagePage, View = view ?? View
    };
}

public sealed class TenantMessagesModel
{
    public TenantMessagesInput Input { get; init; } = new();
    public IReadOnlyList<TenantConversationModel> Conversations { get; set; } = [];
    public TenantConversationModel? Selected { get; set; }
    public IReadOnlyList<TenantMessageModel> Messages { get; set; } = [];
    public AppointmentModel? Appointment { get; set; }
    public int UnreadCount { get; set; }
    public int Total { get; set; }
    public int TotalPages { get; set; } = 1;
    public int MessagePages { get; set; } = 1;
    public bool Unavailable { get; set; }
    public bool LoadError { get; set; }
    public string? SendError { get; set; }
    public string? AttemptedContent { get; set; }
    public string? ContactStamp { get; set; }
    public string DraftKey => Selected?.Id > 0 ? "conversation-" + Selected.Id : "room-" + Input.RentalPostId;
    public bool MobileChat => Selected != null && (Input.ConversationId.HasValue || Input.RentalPostId.HasValue) && Input.View == "chat";
}

public sealed class TenantConversationModel
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public bool IsSupport { get; init; }
    public bool CanSend { get; init; }
    public RentalCardModel? Room { get; init; }
    public int Unread { get; init; }
    public string Preview { get; init; } = "Chưa có tin nhắn";
    public DateTime? LastSentAt { get; init; }
    public string RoomLabel => IsSupport ? "Yêu cầu hỗ trợ #" + Id : Room?.Title ?? "Trao đổi với chủ nhà";
    public string Initials => string.Concat(Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).TakeLast(2)
        .Select(word => StringInfo.GetNextTextElement(word))).ToUpperInvariant();
}

public sealed record TenantMessageModel(int Id, string Content, bool Self, bool IsRead, DateTime SentAt)
{
    // Message.SentAt is UTC in the original model; legacy viewing dates remain wall time.
    public DateTime VietnamTime => DateTime.SpecifyKind(SentAt, DateTimeKind.Utc).AddHours(7);
    public string TimeLabel => VietnamTime.ToString("HH:mm", CultureInfo.InvariantCulture);
    public string DateLabel => VietnamTime.ToString("dddd, dd/MM/yyyy", CultureInfo.GetCultureInfo("vi-VN"));
}
