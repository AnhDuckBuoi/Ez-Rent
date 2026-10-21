using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Services;

public enum MessageWriteStatus { Saved, Missing, ReadOnly, Invalid, StaleContact }
public sealed record MessageSendResult(MessageWriteStatus Status, int ConversationId = 0);

/// <summary>Tenant writes use the original tables. Identity and recipients never come from the form.</summary>
public static class TenantMessagesWriter
{
    public static IQueryable<Conversation> Owned(AppDbContext db, string userId) => db.Conversations.Where(c =>
        (c.InitiatorId == userId || c.RecipientId == userId) && c.InitiatorId != c.RecipientId &&
        (c.ConversationType == ConversationType.TenantLandlord || c.ConversationType == ConversationType.TenantAdmin));

    public static IQueryable<Conversation> ForRoom(AppDbContext db, string userId, int roomId, string ownerId) =>
        Owned(db, userId).Where(c => c.ConversationType == ConversationType.TenantLandlord && c.RentalPostId == roomId &&
            ((c.InitiatorId == userId && c.RecipientId == ownerId) || (c.RecipientId == userId && c.InitiatorId == ownerId)));

    public static IQueryable<RentalPost> ContactableRooms(AppDbContext db, string userId) =>
        db.RentalPosts.VisibleToTenant().Where(p => p.OwnerId != userId && p.Owner.Status == UserStatus.Active);

    // A stale-page check, not an authentication credential. The recipient is still read from the database.
    public static string ContactStamp(RentalPost room) => Convert.ToHexString(SHA256.HashData(
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { room.RentalPostId, room.OwnerId }))));

    public static async Task<MessageSendResult> SendAsync(AppDbContext db, string userId,
        TenantMessageSendInput input, CancellationToken ct = default)
    {
        if (input.ConversationId.HasValue == input.RentalPostId.HasValue || string.IsNullOrWhiteSpace(input.Content) || input.Content.Length > 2000)
            return new(MessageWriteStatus.Invalid);
        // Serializable keeps the room owner and the find-or-create decision stable through the first send.
        // A database conflict rolls back the whole send and is reported to the user, never retried blindly.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, ct)) return new(MessageWriteStatus.ReadOnly);
        Conversation? conversation;
        if (input.ConversationId.HasValue)
        {
            conversation = await Owned(db, userId).Include(c => c.Initiator).Include(c => c.Recipient)
                .SingleOrDefaultAsync(c => c.ConversationId == input.ConversationId, ct);
            if (conversation == null) return new(MessageWriteStatus.Missing);
            if (conversation.Initiator.Status != UserStatus.Active || conversation.Recipient.Status != UserStatus.Active)
                return new(MessageWriteStatus.ReadOnly);
            if (conversation.ConversationType == ConversationType.TenantAdmin)
            {
                var counterpartId = conversation.InitiatorId == userId ? conversation.RecipientId : conversation.InitiatorId;
                if (!await TenantSupportService.ActiveAdministrators(db).AnyAsync(u => u.Id == counterpartId, ct))
                    return new(MessageWriteStatus.ReadOnly);
            }
        }
        else
        {
            var room = await ContactableRooms(db, userId).SingleOrDefaultAsync(p => p.RentalPostId == input.RentalPostId, ct);
            if (room == null) return new(MessageWriteStatus.Missing);
            if (string.IsNullOrEmpty(input.ContactStamp)) return new(MessageWriteStatus.Invalid);
            if (input.ContactStamp != ContactStamp(room)) return new(MessageWriteStatus.StaleContact);
            conversation = await ForRoom(db, userId, room.RentalPostId, room.OwnerId)
                .OrderBy(c => c.ConversationId).FirstOrDefaultAsync(ct);
            if (conversation == null)
            {
                conversation = new Conversation { InitiatorId = userId, RecipientId = room.OwnerId,
                    RentalPostId = room.RentalPostId, ConversationType = ConversationType.TenantLandlord };
                db.Conversations.Add(conversation);
            }
        }
        var now = DateTime.UtcNow;
        conversation.UpdatedAt = now;
        db.Messages.Add(new Message { Conversation = conversation, SenderId = userId,
            Content = input.Content.Trim(), SentAt = now, IsRead = false });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(MessageWriteStatus.Saved, conversation.ConversationId);
    }

    public static async Task<MessageWriteStatus> MarkReadAsync(AppDbContext db, string userId,
        TenantMessageReadInput input, CancellationToken ct = default)
    {
        if (input.MessageIds == null || input.MessageIds.Length is < 1 or > 40 || input.MessageIds.Any(id => id < 1)) return MessageWriteStatus.Invalid;
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, ct)) return MessageWriteStatus.ReadOnly;
        var own = Owned(db, userId);
        if (!await own.AnyAsync(c => c.ConversationId == input.ConversationId, ct)) return MessageWriteStatus.Missing;
        // The same ownership condition is repeated in the UPDATE, including when membership changes after the read.
        await db.Messages.Where(m => m.ConversationId == input.ConversationId && input.MessageIds.Contains(m.MessageId) &&
                own.Any(c => c.ConversationId == m.ConversationId) && !m.IsRead && m.SenderId != userId &&
                (m.SenderId == m.Conversation.InitiatorId || m.SenderId == m.Conversation.RecipientId))
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true), ct);
        return MessageWriteStatus.Saved;
    }
}
