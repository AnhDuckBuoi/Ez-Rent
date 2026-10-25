using EZ_Rent.Areas.Landlord.Services;
using EZ_Rent.Areas.Landlord.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC09 Trò chuyện với người thuê (P04). Dùng đúng bảng Conversation/Message mà trang Tin nhắn của Tenant đang đọc
/// (ConversationType.TenantLandlord, thành viên = Initiator/Recipient), nên tin chủ trọ gửi hiện ngay bên Tenant.
/// Chủ trọ chỉ được mở cuộc trò chuyện với người thuê đã có liên hệ với phòng của mình (lịch hẹn, hợp đồng, sửa chữa).
/// </summary>
public sealed class MessagesController(AppDbContext db, EZ_Rent.Services.ChatRealtime realtime, TimeProvider? clock = null) : LandlordControllerBase(db, clock)
{
    private const int MessagePageSize = 40;
    private IQueryable<Message> MemberMessages => Db.Messages.Where(m => MyConversations.Any(c => c.ConversationId == m.ConversationId) &&
        (m.SenderId == m.Conversation.InitiatorId || m.SenderId == m.Conversation.RecipientId));

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] MessagesInput input)
    {
        if (!ModelState.IsValid) return InvalidQuery(Back(nameof(Index)));
        var me = CurrentUserId;
        var own = MyConversations.AsNoTracking();
        if (input.C.HasValue && !await own.AnyAsync(c => c.ConversationId == input.C.Value))
            return MissingPage("cuộc trò chuyện", Back(nameof(Index)));

        var query = own;
        if (input.Filter == "unread") query = query.Where(c => c.Messages.Any(m => !m.IsRead && m.SenderId != me));
        var keyword = input.Q?.Trim();
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(c => (c.InitiatorId == me ? c.Recipient.FullName : c.Initiator.FullName).Contains(keyword) ||
                (c.RentalPost != null && c.RentalPost.Title.Contains(keyword)));
        var total = await query.CountAsync();
        var page = PagedList<ConversationRow>.ClampPage(input.Page, total, PageSize);
        var rows = await Project(query.OrderByDescending(c => c.Messages.Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt)
                .ThenByDescending(c => c.ConversationId).Skip((page - 1) * PageSize).Take(PageSize), me)
            .ToListAsync();
        input.Page = page;

        var selectedId = input.C ?? rows.FirstOrDefault()?.Id;
        ConversationRow? selected = null;
        var messages = new PagedList<MessageRow>();
        string? phone = null;
        if (selectedId.HasValue)
        {
            selected = rows.FirstOrDefault(r => r.Id == selectedId) ??
                await Project(own.Where(c => c.ConversationId == selectedId), me).FirstOrDefaultAsync();
            if (selected != null)
            {
                var history = MemberMessages.AsNoTracking().Where(m => m.ConversationId == selected.Id);
                var count = await history.CountAsync();
                var mp = PagedList<MessageRow>.ClampPage(input.MessagePage, count, MessagePageSize);
                input.MessagePage = mp;
                // Trang 1 là 40 tin mới nhất; hiển thị theo thứ tự thời gian.
                var items = await history.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId)
                    .Skip((mp - 1) * MessagePageSize).Take(MessagePageSize)
                    .Select(m => new MessageRow(m.MessageId, m.Content, m.SenderId == me, m.SentAt, m.IsRead)).ToListAsync();
                items.Reverse();
                messages = new PagedList<MessageRow> { Items = items, Page = mp, PageSize = MessagePageSize, Total = count };
                phone = await Db.Users.AsNoTracking().Where(u => u.Id == selected.OtherId).Select(u => u.PhoneNumber).FirstOrDefaultAsync();
            }
        }
        var model = new MessagesModel
        {
            Input = input, Selected = selected, Messages = messages, OtherPhone = phone,
            CanSend = selected != null && await Db.Users.AnyAsync(u => u.Id == selected.OtherId && u.Status == UserStatus.Active),
            Conversations = new PagedList<ConversationRow> { Items = rows, Page = page, PageSize = PageSize, Total = total },
            TotalUnread = await own.SelectMany(c => c.Messages).CountAsync(m => !m.IsRead && m.SenderId != me)
        };
        return PageOrRegion("Index", "_Inbox", model);
    }

    [HttpPost, RequestSizeLimit(32768)]
    public async Task<IActionResult> Send([FromRoute] int id, SendMessageInput input)
    {
        var back = Back(nameof(Index), new { c = id });
        var me = CurrentUserId;
        var conversation = await MyConversations.FirstOrDefaultAsync(c => c.ConversationId == id);
        if (conversation == null) return MissingPage("cuộc trò chuyện", Back(nameof(Index)));
        var content = input.Content?.Trim() ?? "";
        if (!ModelState.IsValid || content.Length is 0 or > 2000)
            return await SendFailure(id, input.Content, 400, "Nhập nội dung từ 1 đến 2.000 ký tự.");
        var otherId = conversation.InitiatorId == me ? conversation.RecipientId : conversation.InitiatorId;
        if (!await Db.Users.AnyAsync(u => u.Id == otherId && u.Status == UserStatus.Active))
            return await SendFailure(id, input.Content, 409, "Tài khoản người nhận hiện không hoạt động.");
        var message = new Message { ConversationId = id, SenderId = me, Content = content, IsRead = false, SentAt = UtcNow };
        Db.Messages.Add(message);
        conversation.UpdatedAt = UtcNow;
        try { await Db.SaveChangesAsync(); }
        catch (Exception ex) when (ex is System.Data.Common.DbException or DbUpdateException)
        {
            Log.LogError(ex, "Không hoàn tất gửi tin chủ trọ.");
            return await SendFailure(id, input.Content, 503, "Chưa xác nhận được kết quả gửi. Hãy tải tin mới để kiểm tra trước khi gửi lại.");
        }
        await realtime.ChangedAsync(id);
        if (IsAjax) return Json(new { ok = true, message = "Đã gửi.", item = Dto(new MessageRow(message.MessageId, content, true, message.SentAt, false)) });
        return LocalRedirect(back + "#composer");
    }

    /// <summary>AJAX: lấy tin mới hơn tin cuối đang hiển thị (trang tự kiểm tra mỗi 15 giây khi đang mở).</summary>
    [HttpGet]
    public async Task<IActionResult> Since([FromRoute] int id, [FromQuery] int after)
    {
        var me = CurrentUserId;
        if (!await MyConversations.AnyAsync(c => c.ConversationId == id)) { Response.StatusCode = 404; return Json(new { ok = false }); }
        var items = await MemberMessages.AsNoTracking().Where(m => m.ConversationId == id && m.MessageId > after)
            .OrderBy(m => m.SentAt).ThenBy(m => m.MessageId).Take(100)
            .Select(m => new MessageRow(m.MessageId, m.Content, m.SenderId == me, m.SentAt, m.IsRead)).ToListAsync();
        return Json(new { ok = true, items = items.Select(Dto) });
    }

    [HttpPost]
    public async Task<IActionResult> Read(EZ_Rent.Areas.Tenant.ViewModels.TenantMessageReadInput input)
    {
        if (!ModelState.IsValid) return BadRequest();
        if (!await MyConversations.AnyAsync(c => c.ConversationId == input.ConversationId)) return NotFound();
        var result = await EZ_Rent.Areas.Tenant.Services.TenantMessagesWriter.MarkReadAsync(Db, CurrentUserId, input);
        if (result != EZ_Rent.Areas.Tenant.Services.MessageWriteStatus.Saved) return BadRequest();
        await realtime.ChangedAsync(input.ConversationId);
        if (IsAjax) return Json(new { ok = true });
        return RedirectToAction(nameof(Index), new { c = input.ConversationId, input.MessagePage });
    }

    /// <summary>Mở (hoặc tạo) cuộc trò chuyện với người thuê về một phòng của mình.</summary>
    [HttpPost]
    public async Task<IActionResult> Start([FromForm] int roomId, [FromForm] string tenantId)
    {
        var me = CurrentUserId;
        var room = await MyPosts.AsNoTracking().Where(p => p.RentalPostId == roomId).Select(p => new { p.RentalPostId }).FirstOrDefaultAsync();
        if (room == null || string.IsNullOrWhiteSpace(tenantId) || tenantId == me) return MissingPage("phòng hoặc người thuê", Back(nameof(Index)));
        var related = await Db.ViewingAppointments.AnyAsync(a => a.RentalPostId == roomId && a.TenantId == tenantId) ||
            await Db.RentalContracts.AnyAsync(c => c.RentalPostId == roomId && c.TenantId == tenantId && c.LandlordId == me) ||
            await Db.RepairRequests.AnyAsync(r => r.RentalPostId == roomId && r.TenantId == tenantId) ||
            await Db.RentalRequests.AnyAsync(r => r.RentalPostId == roomId && r.TenantId == tenantId);
        if (!related)
            return Fail(StatusCodes.Status403Forbidden, "Chỉ nhắn được cho người thuê đã đặt lịch, thuê hoặc báo sự cố ở phòng của bạn.", Back(nameof(Index)));

        var existing = await MyConversations.Where(c => c.RentalPostId == roomId &&
                ((c.InitiatorId == me && c.RecipientId == tenantId) || (c.InitiatorId == tenantId && c.RecipientId == me)))
            .Select(c => (int?)c.ConversationId).FirstOrDefaultAsync();
        if (existing == null)
        {
            var conversation = new Conversation
            {
                InitiatorId = me, RecipientId = tenantId, RentalPostId = roomId,
                ConversationType = ConversationType.TenantLandlord, CreatedAt = UtcNow
            };
            Db.Conversations.Add(conversation);
            await Db.SaveChangesAsync();
            existing = conversation.ConversationId;
        }
        return LocalRedirect(Back(nameof(Index), new { c = existing }) + "#composer");
    }

    private async Task<IActionResult> SendFailure(int id, string? content, int status, string error)
    {
        if (IsAjax) return StatusCode(status, new { ok = false, message = error });
        var page = await Index(new MessagesInput { C = id });
        if (page is ViewResult view && view.Model is MessagesModel model) { model.AttemptedContent = content; model.SendError = error; }
        Response.StatusCode = status;
        return page;
    }

    private static IQueryable<ConversationRow> Project(IQueryable<Conversation> query, string me) => query.Select(c => new ConversationRow
    {
        Id = c.ConversationId,
        OtherId = c.InitiatorId == me ? c.RecipientId : c.InitiatorId,
        OtherName = c.InitiatorId == me ? c.Recipient.FullName : c.Initiator.FullName,
        RoomId = c.RentalPostId,
        RoomTitle = c.RentalPost != null ? c.RentalPost.Title : null,
        LastAt = c.Messages.Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt,
        LastContent = c.Messages.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId).Select(m => m.Content).FirstOrDefault(),
        LastFromMe = c.Messages.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId).Select(m => m.SenderId).FirstOrDefault() == me,
        Unread = c.Messages.Count(m => !m.IsRead && m.SenderId != me)
    });

    private static object Dto(MessageRow m) => new
    {
        id = m.Id, content = m.Content, mine = m.Mine,
        time = LandlordTime.Full(LandlordTime.ToVietnam(m.SentAt))
    };
}