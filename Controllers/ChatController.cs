using System.Security.Claims;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;
using EZ_Rent.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Controllers;

/// <summary>
/// Controller Chat Realtime dùng chung cho cả 3 vai trò (Tenant, Landlord, Admin).
/// Cung cấp giao diện dùng chung Views/Chat/Index.cshtml và các endpoint JSON/Form,
/// uỷ quyền xử lý nghiệp vụ và phát tín hiệu SignalR qua ChatService.
/// </summary>
[Authorize(Roles = "Tenant,Landlord,Admin")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChatController(ChatService chatService) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

    private string CurrentRole =>
        User.IsInRole("Admin") ? "Admin" :
        User.IsInRole("Landlord") ? "Landlord" : "Tenant";

    private bool IsFetchRequest => Request.Headers["X-Requested-With"].Count > 0;

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] ChatFilterInput input,
        CancellationToken ct = default)
    {
        if (input.Page < 1 || input.MessagePage < 1 || (input.Q != null && input.Q.Length > 100))
            return BadRequest("Bộ lọc hoặc số trang không hợp lệ.");

        var conversations = await chatService.GetConversationsAsync(
            CurrentUserId,
            input.Type,
            input.UnreadOnly,
            input.Q,
            input.Page,
            ct: ct);
        input.Page = conversations.Page;

        var selectedId = input.C ?? conversations.Items.FirstOrDefault()?.Id;
        ChatConversationItem? selected = null;
        ChatMessagePage? messages = null;

        if (selectedId.HasValue)
        {
            messages = await chatService.GetMessagesAsync(
                CurrentUserId,
                selectedId.Value,
                page: input.MessagePage,
                ct: ct);

            if (messages == null)
            {
                if (input.C.HasValue)
                    return NotFound("Không tìm thấy cuộc trò chuyện.");
            }
            else
            {
                input.MessagePage = messages.Page;
                var lastMsg = messages.Items.LastOrDefault();
                selected = conversations.Items.FirstOrDefault(c => c.Id == selectedId.Value) ??
                    new ChatConversationItem(
                        messages.ConversationId,
                        messages.Type,
                        "",
                        messages.OtherName,
                        messages.RoomId,
                        messages.RoomTitle,
                        lastMsg?.Content,
                        lastMsg?.Mine ?? false,
                        lastMsg?.SentAt ?? DateTime.UtcNow,
                        messages.Items.Count(m => !m.Mine && !m.IsRead),
                        messages.CanSend);
            }
        }

        var model = new ChatPageViewModel
        {
            Input = input,
            Conversations = conversations,
            Selected = selected,
            Messages = messages,
            CanSend = messages?.CanSend ?? false,
            CurrentRole = CurrentRole
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Conversations(
        [FromQuery] ConversationType? type = null,
        [FromQuery] bool unreadOnly = false,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (page < 1 || (q != null && q.Length > 100))
            return BadRequest(new { ok = false, message = "Bộ lọc hoặc số trang không hợp lệ." });

        var result = await chatService.GetConversationsAsync(CurrentUserId, type, unreadOnly, q, page, ct: ct);
        return Json(new { ok = true, data = result });
    }

    [HttpGet]
    public async Task<IActionResult> Messages(
        [FromRoute] int id,
        [FromQuery] int? after = null,
        [FromQuery] int page = 1,
        CancellationToken ct = default)
    {
        if (id < 1 || page < 1 || after is < 0)
            return BadRequest(new { ok = false, message = "Tham số truy vấn tin nhắn không hợp lệ." });

        var result = await chatService.GetMessagesAsync(CurrentUserId, id, after, page, ct: ct);
        if (result == null)
            return NotFound(new { ok = false, message = "Không tìm thấy cuộc trò chuyện." });

        return Json(new { ok = true, data = result });
    }

    [HttpPost, RequestSizeLimit(32768)]
    public async Task<IActionResult> Send(
        [FromRoute] int id,
        [FromForm] string? content,
        [FromForm] bool redirect = false,
        CancellationToken ct = default)
    {
        var outcome = await chatService.SendMessageAsync(CurrentUserId, id, content, ct);

        if (redirect && !IsFetchRequest)
        {
            if (outcome.Status == ChatActionStatus.Saved)
                return RedirectToAction(nameof(Index), new { c = id });
            if (outcome.Status == ChatActionStatus.Missing)
                return NotFound("Không tìm thấy cuộc trò chuyện.");

            var error = outcome.Status == ChatActionStatus.ReadOnly
                ? "Cuộc trò chuyện hiện chỉ đọc."
                : "Nhập tin nhắn từ 1 đến 2.000 ký tự.";
            var page = await Index(new ChatFilterInput { C = id }, ct);
            if (page is ViewResult view && view.Model is ChatPageViewModel model)
            {
                model.AttemptedContent = content;
                model.SendError = error;
            }
            Response.StatusCode = outcome.Status == ChatActionStatus.ReadOnly
                ? StatusCodes.Status409Conflict
                : StatusCodes.Status400BadRequest;
            return page;
        }

        return outcome.Status switch
        {
            ChatActionStatus.Saved => Json(new { ok = true, message = "Đã gửi.", item = outcome.Message }),
            ChatActionStatus.Missing => NotFound(new { ok = false, message = "Không tìm thấy cuộc trò chuyện." }),
            ChatActionStatus.ReadOnly => StatusCode(StatusCodes.Status409Conflict, new { ok = false, message = "Cuộc trò chuyện hiện chỉ đọc." }),
            _ => BadRequest(new { ok = false, message = "Nhập tin nhắn từ 1 đến 2.000 ký tự." })
        };
    }

    [HttpPost, RequestSizeLimit(16384)]
    public async Task<IActionResult> Read(
        [FromForm] int conversationId,
        [FromForm] int[]? messageIds,
        [FromForm] int messagePage = 1,
        [FromForm] bool redirect = false,
        CancellationToken ct = default)
    {
        var status = await chatService.MarkReadAsync(CurrentUserId, conversationId, messageIds, ct);

        if (redirect && !IsFetchRequest)
        {
            return status switch
            {
                ChatActionStatus.Saved => RedirectToAction(nameof(Index), new { c = conversationId, messagePage }),
                ChatActionStatus.Missing => NotFound(),
                _ => BadRequest()
            };
        }

        return status switch
        {
            ChatActionStatus.Saved => Json(new { ok = true }),
            ChatActionStatus.Missing => NotFound(new { ok = false, message = "Không tìm thấy cuộc trò chuyện." }),
            ChatActionStatus.ReadOnly => StatusCode(StatusCodes.Status409Conflict, new { ok = false, message = "Cuộc trò chuyện hiện chỉ đọc." }),
            _ => BadRequest(new { ok = false, message = "Danh sách tin nhắn không hợp lệ." })
        };
    }
}
