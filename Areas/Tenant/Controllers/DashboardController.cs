using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Data;
using EZ_Rent.Areas.Tenant.ViewModels;
using System.Data.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
public sealed class DashboardController(AppDbContext context, ILogger<DashboardController> logger) : TenantControllerBase(context)
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = await TenantHomeReader.ReadAsync(Context, CurrentUserId, DateTimeOffset.UtcNow);
        ViewData["UnreadNotifications"] = model.UnreadCount;
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Help()
    {
        ViewData["UnreadNotifications"] = await Context.Notifications.CountAsync(n => n.UserId == CurrentUserId && !n.IsRead);
        return View();
    }

    [HttpGet]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Messages([FromQuery] TenantMessagesInput input, CancellationToken ct)
    {
        if (input.ConversationId.HasValue && input.RentalPostId.HasValue) ModelState.AddModelError("", "Chọn một cuộc trò chuyện hoặc căn phòng.");
        if (!ModelState.IsValid)
        {
            ModelState.Clear();
            ModelState.AddModelError("", "Bộ lọc hoặc số trang không hợp lệ. Hãy mở lại hộp thư và thử tìm kiếm tối đa 100 ký tự.");
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new TenantMessagesModel());
        }
        try
        {
            var model = await TenantMessagesReader.ReadAsync(Context, CurrentUserId, input, ct);
            if (model.Unavailable) Response.StatusCode = StatusCodes.Status404NotFound;
            return View(model);
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Không đọc được hộp thư Tenant.");
            Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return View(new TenantMessagesModel { Input = input, LoadError = true });
        }
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(32768)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> SendMessage(TenantMessageSendInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return await SendFailure(input, 400, "Kiểm tra cuộc trò chuyện hoặc căn phòng và nhập tin nhắn từ 1 đến 2.000 ký tự.", ct);
        try
        {
            var result = await TenantMessagesWriter.SendAsync(Context, CurrentUserId, input, ct);
            if (result.Status != MessageWriteStatus.Saved)
            {
                var (status, message) = result.Status switch
                {
                    MessageWriteStatus.Missing => (404, "Phòng hoặc cuộc trò chuyện không còn khả dụng. Nội dung chưa được gửi."),
                    MessageWriteStatus.ReadOnly => (409, "Cuộc trò chuyện hiện chỉ đọc. Nội dung chưa được gửi."),
                    MessageWriteStatus.StaleContact => (409, "Chủ nhà của căn phòng đã thay đổi. Hãy tải tin mới và kiểm tra người nhận trước khi gửi lại."),
                    _ => (400, "Nhập tin nhắn từ 1 đến 2.000 ký tự và mở lại đúng căn phòng.")
                };
                return await SendFailure(input, status, message, ct);
            }
            var url = Url.Action(nameof(Messages), new { ConversationId = result.ConversationId, View = "chat" })!;
            if (AjaxRequest) return Json(new { url });
            return LocalRedirect(url);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            logger.LogError(ex, "Không hoàn tất thao tác gửi tin Tenant.");
            return await SendFailure(input, 503, "Chưa xác nhận được kết quả gửi. Hãy tải tin mới để kiểm tra trước khi gửi lại; nội dung vẫn được giữ.", ct);
        }
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(16384)]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> ReadMessages(TenantMessageReadInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return BadRequest(new { error = "Danh sách tin nhắn không hợp lệ." });
        try
        {
            var status = await TenantMessagesWriter.MarkReadAsync(Context, CurrentUserId, input, ct);
            if (status != MessageWriteStatus.Saved)
                return StatusCode(status == MessageWriteStatus.Missing ? 404 : status == MessageWriteStatus.Invalid ? 400 : 409,
                    new { error = "Chưa cập nhật được trạng thái đọc." });
            if (!AjaxRequest) return RedirectToAction(nameof(Messages), new { input.ConversationId, input.MessagePage, View = "chat" });
            var own = TenantMessagesWriter.Owned(Context, CurrentUserId);
            var unreadConversations = await own.CountAsync(c => c.Messages.Any(m => !m.IsRead && m.SenderId != CurrentUserId &&
                (m.SenderId == c.InitiatorId || m.SenderId == c.RecipientId)), ct);
            var unreadMessages = await Context.Messages.CountAsync(m => m.ConversationId == input.ConversationId && !m.IsRead &&
                own.Any(c => c.ConversationId == m.ConversationId) && m.SenderId != CurrentUserId &&
                (m.SenderId == m.Conversation.InitiatorId || m.SenderId == m.Conversation.RecipientId), ct);
            return Json(new { unreadConversations, unreadMessages });
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            logger.LogError(ex, "Không cập nhật được trạng thái đọc tin Tenant.");
            return StatusCode(503, new { error = "Chưa cập nhật được trạng thái đọc. Hãy thử lại sau." });
        }
    }

    private bool AjaxRequest => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private async Task<IActionResult> SendFailure(TenantMessageSendInput input, int status, string message, CancellationToken ct)
    {
        if (AjaxRequest) return StatusCode(status, new { error = message });
        TenantMessagesModel model;
        var target = new TenantMessagesInput { ConversationId = input.ConversationId, RentalPostId = input.RentalPostId };
        try { model = await TenantMessagesReader.ReadAsync(Context, CurrentUserId, target, ct); }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        { model = new TenantMessagesModel { Input = target, LoadError = true }; }
        model.SendError = message;
        model.AttemptedContent = input.Content;
        Response.StatusCode = status;
        return View(nameof(Messages), model);
    }
}
