using System.Data;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Services;

/// <summary>General support uses TenantAdmin conversations, never repair requests.</summary>
public static class TenantSupportService
{
    public static readonly IReadOnlyDictionary<string, string> Categories = new Dictionary<string, string>
    {
        ["room-info"] = "Thông tin phòng không đúng",
        ["viewing"] = "Vấn đề khi xem phòng",
        ["account"] = "Tài khoản và ứng dụng",
        ["other"] = "Vấn đề khác"
    };

    // Resolve recipients from persisted Identity roles, never a posted user ID or a role claim.
    public static IQueryable<ApplicationUser> ActiveAdministrators(AppDbContext db) => db.Users.Where(u =>
        u.Status == UserStatus.Active && db.UserRoles.Any(ur => ur.UserId == u.Id &&
            db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMIN")));

    public static IQueryable<RentalPost> RelatedRooms(AppDbContext db, string userId) => db.RentalPosts.Where(p =>
        (p.ApprovalStatus == ApprovalStatus.Approved && p.RoomStatus == RoomStatus.Available) ||
        db.RentalContracts.Any(c => c.RentalPostId == p.RentalPostId && c.TenantId == userId && c.Status == ContractStatus.Active));

    public static async Task<TenantSupportModel> ReadAsync(AppDbContext db, string userId, TenantSupportInput input, CancellationToken ct)
    {
        var active = await db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, ct);
        var available = active && await ActiveAdministrators(db).AnyAsync(u => u.Id != userId, ct);
        var rooms = await RelatedRooms(db, userId).AsNoTracking().Where(p => p.RentalPostId == input.RentalPostId ||
                db.Favorites.Any(f => f.UserId == userId && f.RentalPostId == p.RentalPostId) ||
                db.RentalContracts.Any(c => c.TenantId == userId && c.RentalPostId == p.RentalPostId && c.Status == ContractStatus.Active))
            .OrderByDescending(p => p.RentalPostId == input.RentalPostId).ThenByDescending(p => p.RentalPostId)
            .Take(30).Select(p => new TenantSupportRoom(p.RentalPostId, p.Title)).ToListAsync(ct);
        return new TenantSupportModel { Input = input, CanSubmit = available, Rooms = rooms,
            Error = !active ? "Tài khoản hiện không thể gửi yêu cầu. Nội dung bạn nhập vẫn được giữ." :
                !available ? "Chưa có tài khoản quản trị hoạt động để tiếp nhận. Bạn có thể giữ nội dung và thử lại sau." : null };
    }

    public static async Task<MessageSendResult> CreateAsync(AppDbContext db, string userId, TenantSupportInput input, CancellationToken ct)
    {
        if (!Categories.ContainsKey(input.Category ?? "") || string.IsNullOrWhiteSpace(input.Subject) || input.Subject.Length > 100 ||
            string.IsNullOrWhiteSpace(input.Description) || input.Description.Length > 1000 || input.RentalPostId is < 1)
            return new(MessageWriteStatus.Invalid);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        if (!await db.Users.AnyAsync(u => u.Id == userId && u.Status == UserStatus.Active, ct)) return new(MessageWriteStatus.ReadOnly);
        var recipientId = await ActiveAdministrators(db).Where(u => u.Id != userId).OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (recipientId == null) return new(MessageWriteStatus.ReadOnly);
        if (input.RentalPostId.HasValue && !await RelatedRooms(db, userId).AnyAsync(p => p.RentalPostId == input.RentalPostId, ct))
            return new(MessageWriteStatus.Missing);

        // Each request is a separate conversation. Category/title are readable message text, not a new ticket schema.
        var now = DateTime.UtcNow;
        var conversation = new Conversation { InitiatorId = userId, RecipientId = recipientId,
            RentalPostId = input.RentalPostId, ConversationType = ConversationType.TenantAdmin, CreatedAt = now, UpdatedAt = now };
        db.Conversations.Add(conversation);
        db.Messages.Add(new Message { Conversation = conversation, SenderId = userId, SentAt = now,
            Content = $"{input.Subject.Trim()}\nVấn đề: {Categories[input.Category!]}\n\n{input.Description.Trim()}" });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return new(MessageWriteStatus.Saved, conversation.ConversationId);
    }
}
