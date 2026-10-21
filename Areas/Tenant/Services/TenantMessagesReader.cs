using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Services;

public static class TenantMessagesReader
{
    public static async Task<TenantMessagesModel> ReadAsync(AppDbContext db, string userId,
        TenantMessagesInput input, CancellationToken ct = default)
    {
        var model = new TenantMessagesModel { Input = input };
        RentalPost? pendingRoom = null;
        var senderActive = await db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, ct);
        if (input.RentalPostId.HasValue)
        {
            if (input.ConversationId.HasValue) { model.Unavailable = true; return model; }
            pendingRoom = await TenantMessagesWriter.ContactableRooms(db, userId).AsNoTracking()
                .Include(p => p.Owner).Include(p => p.RoomImages)
                .SingleOrDefaultAsync(p => p.RentalPostId == input.RentalPostId, ct);
            if (pendingRoom == null) { model.Unavailable = true; return model; }
            var existing = await TenantMessagesWriter.ForRoom(db, userId, pendingRoom.RentalPostId, pendingRoom.OwnerId)
                .OrderBy(c => c.ConversationId).Select(c => (int?)c.ConversationId).FirstOrDefaultAsync(ct);
            if (existing.HasValue) { input.ConversationId = existing; input.RentalPostId = null; pendingRoom = null; }
        }
        // Membership and conversation kind are checked before selecting by ID or reading messages.
        var own = TenantMessagesWriter.Owned(db, userId).AsNoTracking();
        if (input.ConversationId.HasValue && !await own.AnyAsync(c => c.ConversationId == input.ConversationId, ct))
        { model.Unavailable = true; return model; }

        model.UnreadCount = await own.CountAsync(c => c.Messages.Any(m => !m.IsRead && m.SenderId != userId &&
            (m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId)), ct);
        var query = own;
        if (input.Filter == "unread") query = query.Where(c => c.Messages.Any(m => !m.IsRead && m.SenderId != userId &&
            (m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId)));
        input.Query = input.Query?.Trim();
        if (!string.IsNullOrEmpty(input.Query)) query = query.Where(c =>
            (c.InitiatorId == userId ? c.Recipient.FullName : c.Initiator.FullName).Contains(input.Query) ||
            (c.RentalPost != null && c.RentalPost.ApprovalStatus == ApprovalStatus.Approved &&
                c.RentalPost.RoomStatus == RoomStatus.Available && c.RentalPost.Title.Contains(input.Query)));
        model.Total = await query.CountAsync(ct);
        model.TotalPages = Math.Max(1, (int)Math.Ceiling(model.Total / 10d));
        input.Page = Math.Min(input.Page, model.TotalPages);
        var ids = await query.OrderByDescending(c => c.Messages
                .Where(m => m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId)
                .Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt)
            .ThenByDescending(c => c.ConversationId).Skip((input.Page - 1) * 10).Take(10)
            .Select(c => c.ConversationId).ToListAsync(ct);
        var rows = await own.Where(c => ids.Contains(c.ConversationId))
            .Include(c => c.Initiator).Include(c => c.Recipient)
            .Include(c => c.RentalPost).ThenInclude(p => p!.RoomImages).AsSplitQuery().ToListAsync(ct);
        var selectedId = input.ConversationId ?? (pendingRoom != null ? 0 : ids.FirstOrDefault());
        var selected = rows.FirstOrDefault(c => c.ConversationId == selectedId);
        if (selected == null && input.ConversationId.HasValue)
            selected = await own.Include(c => c.Initiator).Include(c => c.Recipient)
                .Include(c => c.RentalPost).ThenInclude(p => p!.RoomImages)
                .AsSplitQuery().SingleAsync(c => c.ConversationId == selectedId, ct);
        if (selected != null && rows.All(c => c.ConversationId != selected.ConversationId)) rows.Add(selected);
        var readIds = rows.Select(c => c.ConversationId).ToArray();
        var memberMessages = db.Messages.AsNoTracking().Where(m => readIds.Contains(m.ConversationId) &&
            (m.SenderId == m.Conversation.InitiatorId || m.SenderId == m.Conversation.RecipientId));
        var unread = await memberMessages.Where(m => m.SenderId != userId && !m.IsRead)
            .GroupBy(m => m.ConversationId).Select(g => new { Id = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Id, g => g.Count, ct);
        // Fetch only one preview per conversation, never its whole history.
        var latestIds = await memberMessages.GroupBy(m => m.ConversationId)
            .Select(g => g.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId).Select(m => m.MessageId).First())
            .ToListAsync(ct);
        var latest = await db.Messages.AsNoTracking().Where(m => latestIds.Contains(m.MessageId))
            .ToDictionaryAsync(m => m.ConversationId, ct);
        TenantConversationModel Map(Conversation c)
        {
            latest.TryGetValue(c.ConversationId, out var last);
            var support = c.ConversationType == ConversationType.TenantAdmin;
            var name = c.InitiatorId == userId ? c.Recipient.FullName : c.Initiator.FullName;
            return new()
            {
                Id = c.ConversationId, Name = string.IsNullOrWhiteSpace(name) ? (support ? "Hỗ trợ EZ Rent" : "Chủ nhà") : name,
                IsSupport = support, Room = c.RentalPost == null ? null : TenantRentalDiscovery.Card(c.RentalPost, false),
                CanSend = !support && senderActive && c.Initiator.Status == UserStatus.Active && c.Recipient.Status == UserStatus.Active,
                Unread = unread.GetValueOrDefault(c.ConversationId), LastSentAt = last?.SentAt,
                Preview = last == null ? "Chưa có tin nhắn" : (last.SenderId == userId ? "Bạn: " : "") + last.Content
            };
        }
        model.Conversations = ids.Select(id => Map(rows.Single(c => c.ConversationId == id))).ToList();
        if (pendingRoom != null)
        {
            model.ContactStamp = TenantMessagesWriter.ContactStamp(pendingRoom);
            model.Selected = new TenantConversationModel { Name = string.IsNullOrWhiteSpace(pendingRoom.Owner.FullName) ? "Chủ nhà" : pendingRoom.Owner.FullName,
                Room = TenantRentalDiscovery.Card(pendingRoom, false), CanSend = senderActive };
            return model;
        }
        if (selected == null) return model;
        model.Selected = Map(selected);
        var history = memberMessages.Where(m => m.ConversationId == selectedId);
        var count = await history.CountAsync(ct);
        model.MessagePages = Math.Max(1, (int)Math.Ceiling(count / 40d));
        input.MessagePage = Math.Min(input.MessagePage, model.MessagePages);
        var messages = await history.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId)
            .Skip((input.MessagePage - 1) * 40).Take(40)
            .Select(m => new TenantMessageModel(m.MessageId, m.Content, m.SenderId == userId, m.IsRead, m.SentAt)).ToListAsync(ct);
        model.Messages = messages.AsEnumerable().Reverse().ToList();
        if (model.Selected.Room?.IsAvailable == true && !model.Selected.IsSupport)
        {
            var now = DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime;
            var appointment = await db.ViewingAppointments.AsNoTracking().Where(a => a.TenantId == userId &&
                    a.RentalPostId == selected.RentalPostId && a.Status == ViewingAppointmentStatus.Confirmed && a.ViewingDate >= now)
                .OrderBy(a => a.ViewingDate).ThenBy(a => a.ViewingAppointmentId).Include(a => a.RentalPost).FirstOrDefaultAsync(ct);
            model.Appointment = appointment == null ? null : AppointmentModel.From(appointment);
        }
        return model;
    }
}
