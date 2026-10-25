using System.Globalization;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;

namespace EZ_Rent.ViewModels;

public sealed class ChatFilterInput
{
    public int? C { get; set; }
    public ConversationType? Type { get; set; }
    public bool UnreadOnly { get; set; }
    public string? Q { get; set; }
    public int Page { get; set; } = 1;
    public int MessagePage { get; set; } = 1;
}

public sealed class ChatPageViewModel
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);

    public ChatFilterInput Input { get; init; } = new();
    public ChatConversationPage Conversations { get; init; } = new([], 1, ChatService.DefaultConversationPageSize, 0, 1, 0);
    public ChatConversationItem? Selected { get; init; }
    public ChatMessagePage? Messages { get; init; }
    public bool CanSend { get; init; }
    public string? SendError { get; set; }
    public string? AttemptedContent { get; set; }
    public string CurrentRole { get; init; } = "";

    public static string Stamp(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc)
            .Add(VietnamOffset)
            .ToString("dd/MM/yyyy · HH:mm", CultureInfo.InvariantCulture);

    public static string TypeBadge(ConversationType type) => type switch
    {
        ConversationType.TenantLandlord => "Thuê phòng",
        ConversationType.TenantAdmin => "Hỗ trợ Người thuê",
        ConversationType.LandlordAdmin => "Hỗ trợ Chủ trọ",
        _ => "Hội thoại"
    };
}
