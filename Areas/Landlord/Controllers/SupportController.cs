using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Areas.Landlord.ViewModels.Support;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services.Support;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// R18 / UC10 phía chủ trọ: gửi yêu cầu hỗ trợ, phản ánh tới quản trị viên và xem phản hồi.
/// Cùng cách làm với trang Hỗ trợ của Tenant (LandlordSupportService ↔ TenantSupportService), khác ở loại hội thoại LandlordAdmin.
/// Phía Admin trả lời bằng Message trong cùng cuộc trò chuyện (hộp thư Admin chưa có trong main – xem docs).
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class SupportController(AppDbContext db, TimeProvider? clock = null) : LandlordAreaController(db, clock)
{
    private IQueryable<Conversation> MySupport => Db.Conversations.Where(c =>
        c.ConversationType == ConversationType.LandlordAdmin && c.InitiatorId == CurrentUserId);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        if (page < 1) return InvalidQuery(Back(nameof(Index)));
        var me = CurrentUserId;
        var items = await PageAsync(MySupport.AsNoTracking()
            .OrderByDescending(c => c.Messages.Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt).ThenByDescending(c => c.ConversationId)
            .Select(c => new SupportRow
            {
                Id = c.ConversationId, RoomTitle = c.RentalPost != null ? c.RentalPost.Title : null, CreatedAt = c.CreatedAt,
                FirstMessage = c.Messages.OrderBy(m => m.SentAt).ThenBy(m => m.MessageId).Select(m => m.Content).FirstOrDefault() ?? "",
                LastAt = c.Messages.Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt,
                LastFromMe = c.Messages.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId).Select(m => m.SenderId).FirstOrDefault() == me,
                Unread = c.Messages.Count(m => !m.IsRead && m.SenderId != me)
            }), page);
        return PageOrRegion("Index", "_SupportList", new SupportListModel { Items = items, Pager = Pager(items) });
    }

    [HttpGet]
    public async Task<IActionResult> Create([FromQuery] int? roomId) =>
        View("Create", await FormAsync(new SupportInput { RoomId = roomId }));

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] SupportInput input)
    {
        if (!ModelState.IsValid) return View("Create", await FormAsync(input));
        var (result, conversationId) = await LandlordSupportService.CreateAsync(Db, CurrentUserId, input.Category, input.Subject,
            input.Content, input.RoomId, UtcNow, HttpContext.RequestAborted);
        switch (result)
        {
            case SupportCreateResult.Saved:
                return Done("Đã gửi yêu cầu tới quản trị viên. Phản hồi sẽ hiện trong mục Hỗ trợ.", Back(nameof(Details), new { id = conversationId }));
            case SupportCreateResult.NotYourRoom:
                ModelState.AddModelError("Input.RoomId", "Chọn phòng của bạn."); break;
            case SupportCreateResult.NoAdministrator:
                ModelState.AddModelError("", "Chưa có tài khoản quản trị hoạt động để tiếp nhận. Nội dung bạn nhập vẫn được giữ, hãy thử lại sau."); break;
            default:
                ModelState.AddModelError("", "Thông tin yêu cầu chưa hợp lệ."); break;
        }
        return View("Create", await FormAsync(input));
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var me = CurrentUserId;
        var c = await MySupport.AsNoTracking().Where(x => x.ConversationId == id)
            .Select(x => new { x.ConversationId, x.CreatedAt, Room = x.RentalPost != null ? x.RentalPost.Title : null, Admin = x.Recipient.FullName })
            .FirstOrDefaultAsync();
        if (c == null) return NotFoundPage("yêu cầu hỗ trợ", Back(nameof(Index)));
        await Db.Messages.Where(m => m.ConversationId == id && m.SenderId != me && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        var messages = await Db.Messages.AsNoTracking().Where(m => m.ConversationId == id)
            .OrderBy(m => m.SentAt).ThenBy(m => m.MessageId).Take(500)
            .Select(m => new SupportMessageRow(m.MessageId, m.Content, m.SenderId == me, m.SentAt)).ToListAsync();
        return View("Details", new SupportDetailModel
        {
            Item = new SupportRow { Id = c.ConversationId, RoomTitle = c.Room, CreatedAt = c.CreatedAt, FirstMessage = messages.FirstOrDefault()?.Content ?? "" },
            AdminName = string.IsNullOrWhiteSpace(c.Admin) ? "Quản trị viên" : c.Admin, Messages = messages
        });
    }

    [HttpPost]
    public async Task<IActionResult> Reply([FromRoute] int id, SupportReplyInput input)
    {
        var back = Back(nameof(Details), new { id });
        var conversation = await MySupport.FirstOrDefaultAsync(x => x.ConversationId == id);
        if (conversation == null) return NotFoundPage("yêu cầu hỗ trợ", Back(nameof(Index)));
        var content = input.Content?.Trim() ?? "";
        if (!ModelState.IsValid || content.Length == 0) return Fail(StatusCodes.Status400BadRequest, "Nhập nội dung (tối đa 1000 ký tự).", back);
        Db.Messages.Add(new Message { ConversationId = id, SenderId = CurrentUserId, Content = content, SentAt = UtcNow, IsRead = false });
        conversation.UpdatedAt = UtcNow;
        await Db.SaveChangesAsync();
        return Done("Đã gửi bổ sung cho quản trị viên.", back);
    }

    private async Task<SupportFormModel> FormAsync(SupportInput input) => new()
    {
        Input = input,
        Rooms = await MyPosts.AsNoTracking().OrderBy(p => p.Title)
            .Select(p => new OptionItem(p.RentalPostId.ToString(), p.Title, null)).ToListAsync(),
        AdministratorAvailable = await LandlordSupportService.ActiveAdministrators(Db).AnyAsync(u => u.Id != CurrentUserId)
    };
}
