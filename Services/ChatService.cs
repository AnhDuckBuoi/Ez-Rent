using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Services;

public enum ChatActionStatus { Saved, Missing, ReadOnly, Invalid }

public sealed record ChatConversationItem(
    int Id,
    ConversationType Type,
    string OtherUserId,
    string OtherName,
    int? RoomId,
    string? RoomTitle,
    string? LastContent,
    bool LastFromMe,
    DateTime LastAt,
    int UnreadCount,
    bool CanSend);

public sealed record ChatConversationPage(
    IReadOnlyList<ChatConversationItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages,
    int TotalUnreadMessages);

public sealed record ChatMessageItem(
    int Id,
    string Content,
    bool Mine,
    bool IsRead,
    DateTime SentAt);

public sealed record ChatMessagePage(
    int ConversationId,
    ConversationType Type,
    string OtherName,
    int? RoomId,
    string? RoomTitle,
    bool CanSend,
    IReadOnlyList<ChatMessageItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record ChatSendOutcome(
    ChatActionStatus Status,
    ChatMessageItem? Message = null);

/// <summary>
/// Dịch vụ Chat & Realtime dùng chung cho cả 3 vai trò (Tenant, Landlord, Admin).
/// Quản lý đọc hội thoại, lấy tin nhắn, gửi tin nhắn, đánh dấu đã xem và phát tín hiệu SignalR qua ChatRealtime.
/// </summary>
public sealed class ChatService(AppDbContext db, ChatRealtime realtime)
{
    public const int DefaultConversationPageSize = 10;
    public const int DefaultMessagePageSize = 40;

    public IQueryable<Conversation> MemberConversations(string userId) =>
        db.Conversations.Where(c =>
            (c.InitiatorId == userId || c.RecipientId == userId) &&
            c.InitiatorId != c.RecipientId &&
            (c.ConversationType == ConversationType.TenantLandlord ||
             c.ConversationType == ConversationType.TenantAdmin ||
             c.ConversationType == ConversationType.LandlordAdmin));

    public IQueryable<Message> MemberMessages(string userId) =>
        db.Messages.Where(m =>
            MemberConversations(userId).Any(c => c.ConversationId == m.ConversationId) &&
            (m.SenderId == m.Conversation.InitiatorId || m.SenderId == m.Conversation.RecipientId));

    public Task NotifyConversationChangedAsync(int conversationId) =>
        realtime.ChangedAsync(conversationId);

    public async Task<ChatConversationPage> GetConversationsAsync(
        string userId,
        ConversationType? type = null,
        bool unreadOnly = false,
        string? query = null,
        int page = 1,
        int pageSize = DefaultConversationPageSize,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 50);
        var own = MemberConversations(userId).AsNoTracking();
        var senderActive = await db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, ct);

        var filtered = own;
        if (type.HasValue)
            filtered = filtered.Where(c => c.ConversationType == type.Value);
        if (unreadOnly)
            filtered = filtered.Where(c => c.Messages.Any(m => !m.IsRead && m.SenderId != userId &&
                (m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId)));

        var keyword = query?.Trim();
        if (!string.IsNullOrEmpty(keyword))
        {
            filtered = filtered.Where(c =>
                (c.InitiatorId == userId ? c.Recipient.FullName : c.Initiator.FullName).Contains(keyword) ||
                (c.RentalPost != null && c.RentalPost.Title.Contains(keyword)));
        }

        var totalCount = await filtered.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var rows = await filtered
            .OrderByDescending(c => c.Messages
                .Where(m => m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId)
                .Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt)
            .ThenByDescending(c => c.ConversationId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.ConversationId,
                c.ConversationType,
                OtherId = c.InitiatorId == userId ? c.RecipientId : c.InitiatorId,
                OtherName = c.InitiatorId == userId ? c.Recipient.FullName : c.Initiator.FullName,
                OtherActive = (c.InitiatorId == userId ? c.Recipient.Status : c.Initiator.Status) == UserStatus.Active,
                c.RentalPostId,
                RoomTitle = c.RentalPost != null ? c.RentalPost.Title : null,
                LastAt = c.Messages
                    .Where(m => m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId)
                    .Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt,
                LastContent = c.Messages
                    .Where(m => m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId)
                    .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId)
                    .Select(m => m.Content)
                    .FirstOrDefault(),
                LastSenderId = c.Messages
                    .Where(m => m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId)
                    .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId)
                    .Select(m => m.SenderId)
                    .FirstOrDefault(),
                UnreadCount = c.Messages.Count(m => !m.IsRead && m.SenderId != userId &&
                    (m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId))
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new ChatConversationItem(
            r.ConversationId,
            r.ConversationType,
            r.OtherId,
            string.IsNullOrWhiteSpace(r.OtherName) ? "Người dùng" : r.OtherName,
            r.RentalPostId,
            r.RoomTitle,
            r.LastContent,
            r.LastSenderId == userId,
            r.LastAt,
            r.UnreadCount,
            senderActive && r.OtherActive)).ToList();

        var totalUnread = await MemberMessages(userId).AsNoTracking()
            .CountAsync(m => !m.IsRead && m.SenderId != userId, ct);

        return new ChatConversationPage(items, page, pageSize, totalCount, totalPages, totalUnread);
    }

    public async Task<ChatMessagePage?> GetMessagesAsync(
        string userId,
        int conversationId,
        int? afterMessageId = null,
        int page = 1,
        int pageSize = DefaultMessagePageSize,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        var conversation = await MemberConversations(userId).AsNoTracking()
            .Where(c => c.ConversationId == conversationId)
            .Select(c => new
            {
                c.ConversationId,
                c.ConversationType,
                OtherName = c.InitiatorId == userId ? c.Recipient.FullName : c.Initiator.FullName,
                BothActive = c.Initiator.Status == UserStatus.Active && c.Recipient.Status == UserStatus.Active,
                c.RentalPostId,
                RoomTitle = c.RentalPost != null ? c.RentalPost.Title : null
            })
            .SingleOrDefaultAsync(ct);

        if (conversation == null) return null;

        var history = MemberMessages(userId).AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (afterMessageId.HasValue)
        {
            var newer = await history
                .Where(m => m.MessageId > afterMessageId.Value)
                .OrderBy(m => m.SentAt).ThenBy(m => m.MessageId)
                .Take(100)
                .Select(m => new ChatMessageItem(m.MessageId, m.Content, m.SenderId == userId, m.IsRead, m.SentAt))
                .ToListAsync(ct);

            return new ChatMessagePage(
                conversation.ConversationId,
                conversation.ConversationType,
                string.IsNullOrWhiteSpace(conversation.OtherName) ? "Người dùng" : conversation.OtherName,
                conversation.RentalPostId,
                conversation.RoomTitle,
                conversation.BothActive,
                newer,
                1,
                newer.Count,
                newer.Count,
                1);
        }

        var totalCount = await history.CountAsync(ct);
        var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await history
            .OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(m => new ChatMessageItem(m.MessageId, m.Content, m.SenderId == userId, m.IsRead, m.SentAt))
            .ToListAsync(ct);
        items.Reverse();

        return new ChatMessagePage(
            conversation.ConversationId,
            conversation.ConversationType,
            string.IsNullOrWhiteSpace(conversation.OtherName) ? "Người dùng" : conversation.OtherName,
            conversation.RentalPostId,
            conversation.RoomTitle,
            conversation.BothActive,
            items,
            page,
            pageSize,
            totalCount,
            totalPages);
    }

    public async Task<ChatSendOutcome> SendMessageAsync(
        string userId,
        int conversationId,
        string? content,
        CancellationToken ct = default)
    {
        var trimmed = content?.Trim() ?? "";
        if (conversationId < 1 || trimmed.Length is 0 or > 2000)
            return new(ChatActionStatus.Invalid);

        if (!await db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, ct))
            return new(ChatActionStatus.ReadOnly);

        var conversation = await MemberConversations(userId)
            .Include(c => c.Initiator)
            .Include(c => c.Recipient)
            .SingleOrDefaultAsync(c => c.ConversationId == conversationId, ct);

        if (conversation == null)
            return new(ChatActionStatus.Missing);

        if (conversation.Initiator.Status != UserStatus.Active || conversation.Recipient.Status != UserStatus.Active)
            return new(ChatActionStatus.ReadOnly);

        var now = DateTime.UtcNow;
        var message = new Message
        {
            ConversationId = conversationId,
            SenderId = userId,
            Content = trimmed,
            IsRead = false,
            SentAt = now
        };
        db.Messages.Add(message);
        conversation.UpdatedAt = now;
        await db.SaveChangesAsync(ct);

        await realtime.ChangedAsync(conversationId);
        return new(ChatActionStatus.Saved, new ChatMessageItem(message.MessageId, trimmed, true, false, now));
    }

    public async Task<ChatActionStatus> MarkReadAsync(
        string userId,
        int conversationId,
        int[]? messageIds,
        CancellationToken ct = default)
    {
        if (conversationId < 1 || messageIds == null || messageIds.Length is < 1 or > 40 || messageIds.Any(id => id < 1))
            return ChatActionStatus.Invalid;

        if (!await db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, ct))
            return ChatActionStatus.ReadOnly;

        var own = MemberConversations(userId);
        if (!await own.AnyAsync(c => c.ConversationId == conversationId, ct))
            return ChatActionStatus.Missing;

        await db.Messages.Where(m =>
                m.ConversationId == conversationId &&
                messageIds.Contains(m.MessageId) &&
                own.Any(c => c.ConversationId == m.ConversationId) &&
                !m.IsRead &&
                m.SenderId != userId &&
                (m.SenderId == m.Conversation.InitiatorId || m.SenderId == m.Conversation.RecipientId))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true), ct);

        await realtime.ChangedAsync(conversationId);
        return ChatActionStatus.Saved;
    }
}
