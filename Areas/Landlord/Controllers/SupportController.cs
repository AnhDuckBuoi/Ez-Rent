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
/// R18 / UC10 (phía chủ trọ): gửi yêu cầu hỗ trợ, phản ánh, báo cáo tới quản trị viên và xem phản hồi.
/// Dùng bảng Conversation/Message có sẵn với ConversationType.LandlordAdmin:
///   InitiatorId = chủ trọ, RecipientId = một tài khoản Admin đang hoạt động, RentalPostId = phòng liên quan (nếu có).
/// Tin đầu tiên có dạng "[Loại] Tiêu đề\n\nNội dung". Phía Admin (R19) chỉ cần đọc các Conversation loại LandlordAdmin
/// và trả lời bằng Message trong cùng cuộc trò chuyện; trang này sẽ hiện phản hồi đó.
/// </summary>
public sealed class SupportController(AppDbContext db, TimeProvider? clock = null) : LandlordControllerBase(db, clock)
{
    private IQueryable<Conversation> MySupport => Db.Conversations.Where(c =>
        c.ConversationType == ConversationType.LandlordAdmin && c.InitiatorId == CurrentUserId);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int page = 1)
    {
        if (page < 1) return InvalidQuery(Back(nameof(Index)));
        var me = CurrentUserId;
        var total = await MySupport.CountAsync();
        page = PagedList<SupportRow>.ClampPage(page, total, PageSize);
        var rows = await MySupport.AsNoTracking()
            .OrderByDescending(c => c.Messages.Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt).ThenByDescending(c => c.ConversationId)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(c => new SupportRow
            {
                Id = c.ConversationId, RoomTitle = c.RentalPost != null ? c.RentalPost.Title : null, CreatedAt = c.CreatedAt,
                FirstMessage = c.Messages.OrderBy(m => m.SentAt).ThenBy(m => m.MessageId).Select(m => m.Content).FirstOrDefault() ?? "",
                LastAt = c.Messages.Max(m => (DateTime?)m.SentAt) ?? c.CreatedAt,
                LastFromMe = c.Messages.OrderByDescending(m => m.SentAt).ThenByDescending(m => m.MessageId).Select(m => m.SenderId).FirstOrDefault() == me,
                Unread = c.Messages.Count(m => !m.IsRead && m.SenderId != me),
                Count = c.Messages.Count()
            }).ToListAsync();
        return PageOrRegion("Index", "_SupportList", new SupportListModel
        {
            Items = new PagedList<SupportRow> { Items = rows, Page = page, PageSize = PageSize, Total = total }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create([FromQuery] int? roomId)
    {
        await RoomOptionsAsync();
        return View("Create", new SupportInput { RoomId = roomId });
    }

    [HttpPost]
    public async Task<IActionResult> Create(SupportInput input)
    {
        if (input.RoomId.HasValue && !await MyPosts.AnyAsync(p => p.RentalPostId == input.RoomId.Value))
            ModelState.AddModelError(nameof(input.RoomId), "Chọn phòng của bạn.");
        var admin = await AdminIdAsync();
        if (admin == null) ModelState.AddModelError("", "Hệ thống chưa có tài khoản quản trị viên đang hoạt động để nhận yêu cầu.");
        if (!ModelState.IsValid) { await RoomOptionsAsync(); return View("Create", input); }

        var conversation = new Conversation
        {
            InitiatorId = CurrentUserId, RecipientId = admin!, RentalPostId = input.RoomId,
            ConversationType = ConversationType.LandlordAdmin, CreatedAt = UtcNow
        };
        conversation.Messages.Add(new Message
        {
            SenderId = CurrentUserId, SentAt = UtcNow, IsRead = false,
            Content = $"[{SupportInput.CategoryLabel(input.Category)}] {input.Subject.Trim()}\n\n{input.Content.Trim()}"
        });
        Db.Conversations.Add(conversation);
        await Db.SaveChangesAsync();
        return Done("Đã gửi yêu cầu tới quản trị viên. Phản hồi sẽ hiện trong mục Hỗ trợ.", Back(nameof(Details), new { id = conversation.ConversationId }));
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var me = CurrentUserId;
        var c = await MySupport.AsNoTracking().Where(x => x.ConversationId == id)
            .Select(x => new { x.ConversationId, x.CreatedAt, Room = x.RentalPost != null ? x.RentalPost.Title : null, Admin = x.Recipient.FullName })
            .FirstOrDefaultAsync();
        if (c == null) return MissingPage("yêu cầu hỗ trợ", Back(nameof(Index)));
        await Db.Messages.Where(m => m.ConversationId == id && m.SenderId != me && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
        var messages = await Db.Messages.AsNoTracking().Where(m => m.ConversationId == id)
            .OrderBy(m => m.SentAt).ThenBy(m => m.MessageId).Take(500)
            .Select(m => new MessageRow(m.MessageId, m.Content, m.SenderId == me, m.SentAt, m.IsRead)).ToListAsync();
        return View("Details", new SupportDetailModel
        {
            Item = new SupportRow { Id = c.ConversationId, RoomTitle = c.Room, CreatedAt = c.CreatedAt, FirstMessage = messages.FirstOrDefault()?.Content ?? "" },
            AdminName = string.IsNullOrWhiteSpace(c.Admin) ? "Quản trị viên" : c.Admin, Messages = messages
        });
    }

    [HttpPost]
    public async Task<IActionResult> Reply([FromRoute] int id, SendMessageInput input)
    {
        var back = Back(nameof(Details), new { id });
        var c = await MySupport.FirstOrDefaultAsync(x => x.ConversationId == id);
        if (c == null) return MissingPage("yêu cầu hỗ trợ", Back(nameof(Index)));
        var content = input.Content?.Trim() ?? "";
        if (!ModelState.IsValid || content.Length == 0) return Fail(StatusCodes.Status400BadRequest, "Nhập nội dung (tối đa 2000 ký tự).", back);
        Db.Messages.Add(new Message { ConversationId = id, SenderId = CurrentUserId, Content = content, SentAt = UtcNow, IsRead = false });
        await Db.SaveChangesAsync();
        return Done("Đã gửi bổ sung cho quản trị viên.", back);
    }

    private async Task RoomOptionsAsync() =>
        ViewData["Rooms"] = await MyPosts.AsNoTracking().OrderBy(p => p.Title)
            .Select(p => new OptionItem(p.RentalPostId.ToString(), p.Title, null)).ToListAsync();

    /// <summary>Người nhận: tài khoản Admin đang hoạt động được tạo sớm nhất (tài khoản admin mặc định khi seed).</summary>
    private Task<string?> AdminIdAsync() =>
        Db.Users.Where(u => u.Status == UserStatus.Active &&
                Db.UserRoles.Any(ur => ur.UserId == u.Id && Db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "ADMIN")))
            .OrderBy(u => u.CreatedAt).ThenBy(u => u.Id).Select(u => u.Id).FirstOrDefaultAsync();
}
