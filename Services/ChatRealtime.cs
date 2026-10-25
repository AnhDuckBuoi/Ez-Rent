using System.Security.Claims;
using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Services;

// The socket carries invalidation IDs only. Content is fetched through owner-scoped MVC endpoints.
[Authorize(Roles = "Tenant,Landlord,Admin")]
public sealed class ChatHub(AppDbContext db) : Hub
{
    public override async Task OnConnectedAsync()
    {
        var request = Context.GetHttpContext()?.Request;
        var origin = request?.Headers.Origin.ToString();
        if (!string.IsNullOrEmpty(origin) && (!Uri.TryCreate(origin, UriKind.Absolute, out var uri) ||
            request == null || !string.Equals(uri.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(uri.Scheme, request.Scheme, StringComparison.OrdinalIgnoreCase)))
        { Context.Abort(); return; }
        var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id) || !await ChatRealtime.ActiveParticipants(db).AnyAsync(u => u.Id == id))
        { Context.Abort(); return; }
        await base.OnConnectedAsync();
    }
}

public sealed class ChatRealtime(AppDbContext db, IHubContext<ChatHub> hub, ILogger<ChatRealtime> logger)
{
    internal static IQueryable<Models.ApplicationUser> ActiveParticipants(AppDbContext db) => db.Users.Where(u =>
        u.Status == UserStatus.Active && db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r =>
            r.Id == ur.RoleId && (r.NormalizedName == "TENANT" || r.NormalizedName == "LANDLORD" || r.NormalizedName == "ADMIN"))));

    public async Task ChangedAsync(int conversationId)
    {
        try
        {
            var conversation = await db.Conversations.AsNoTracking().Where(c => c.ConversationId == conversationId &&
                    c.InitiatorId != c.RecipientId && (c.ConversationType == ConversationType.TenantLandlord || c.ConversationType == ConversationType.TenantAdmin || c.ConversationType == ConversationType.LandlordAdmin))
                .Select(c => new { c.InitiatorId, c.RecipientId }).SingleOrDefaultAsync();
            if (conversation == null) return;
            // Recheck persisted roles/status on each delivery; socket claims may be stale after an account change.
            var ids = await ActiveParticipants(db).Where(u => u.Id == conversation.InitiatorId || u.Id == conversation.RecipientId)
                .Select(u => u.Id).ToListAsync();
            await hub.Clients.Users(ids).SendAsync("ConversationChanged", conversationId);
        }
        catch (Exception ex)
        {
            // A notification failure never turns an already committed message into a failed send.
            logger.LogWarning(ex, "Không phát được cập nhật hội thoại {ConversationId}.", conversationId);
        }
    }
}
