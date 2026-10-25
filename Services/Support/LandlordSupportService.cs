using System.Data;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Services.Support;

public enum SupportCreateResult { Saved, Invalid, NoAdministrator, NotYourRoom }

/// <summary>
/// R18/UC10 phía chủ trọ: yêu cầu hỗ trợ gửi quản trị viên bằng hội thoại ConversationType.LandlordAdmin.
/// Làm giống hệt TenantSupportService của phía Tenant: người nhận lấy từ vai trò Admin đang hoạt động trong DB (không nhận ID từ form),
/// mỗi yêu cầu là một cuộc trò chuyện, tin đầu có dạng "Tiêu đề\nVấn đề: Loại\n\nNội dung", ghi trong transaction Serializable.
/// </summary>
public static class LandlordSupportService
{
    public static readonly IReadOnlyDictionary<string, string> Categories = new Dictionary<string, string>
    {
        ["listing"] = "Tin đăng và kiểm duyệt",
        ["tenant"] = "Vấn đề với người thuê",
        ["payment"] = "Hóa đơn và thanh toán",
        ["account"] = "Tài khoản và ứng dụng",
        ["other"] = "Vấn đề khác"
    };

    public static IQueryable<ApplicationUser> ActiveAdministrators(AppDbContext db) => db.Users.Where(u =>
        u.Status == UserStatus.Active && db.UserRoles.Any(ur => ur.UserId == u.Id &&
            db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMIN")));

    public static async Task<(SupportCreateResult Result, int ConversationId)> CreateAsync(AppDbContext db, string landlordId,
        string category, string subject, string description, int? roomId, DateTime utcNow, CancellationToken ct)
    {
        subject = subject.Trim(); description = description.Trim();
        if (!Categories.ContainsKey(category) || subject.Length is < 5 or > 100 || description.Length is < 10 or > 1000)
            return (SupportCreateResult.Invalid, 0);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var recipient = await ActiveAdministrators(db).Where(u => u.Id != landlordId).OrderBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync(ct);
        if (recipient == null) return (SupportCreateResult.NoAdministrator, 0);
        if (roomId.HasValue && !await db.RentalPosts.AnyAsync(p => p.RentalPostId == roomId && p.OwnerId == landlordId, ct))
            return (SupportCreateResult.NotYourRoom, 0);
        var conversation = new Conversation
        {
            InitiatorId = landlordId, RecipientId = recipient, RentalPostId = roomId,
            ConversationType = ConversationType.LandlordAdmin, CreatedAt = utcNow, UpdatedAt = utcNow
        };
        db.Conversations.Add(conversation);
        db.Messages.Add(new Message
        {
            Conversation = conversation, SenderId = landlordId, SentAt = utcNow,
            Content = $"{subject}\nVấn đề: {Categories[category]}\n\n{description}"
        });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return (SupportCreateResult.Saved, conversation.ConversationId);
    }
}
