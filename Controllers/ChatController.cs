using System.Security.Claims;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Controllers;

/// <summary>
/// Controller Chat Realtime dùng chung cho cả 3 vai trò (Tenant, Landlord, Admin).
/// Uỷ quyền xử lý nghiệp vụ và phát tín hiệu SignalR qua ChatService.
/// </summary>
[Authorize(Roles = "Tenant,Landlord,Admin")]
[AutoValidateAntiforgeryToken]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ChatController(ChatService chatService) : Controller
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";

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
        CancellationToken ct = default)
    {
        var outcome = await chatService.SendMessageAsync(CurrentUserId, id, content, ct);
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
        CancellationToken ct = default)
    {
        var status = await chatService.MarkReadAsync(CurrentUserId, conversationId, messageIds, ct);
        return status switch
        {
            ChatActionStatus.Saved => Json(new { ok = true }),
            ChatActionStatus.Missing => NotFound(new { ok = false, message = "Không tìm thấy cuộc trò chuyện." }),
            ChatActionStatus.ReadOnly => StatusCode(StatusCodes.Status409Conflict, new { ok = false, message = "Cuộc trò chuyện hiện chỉ đọc." }),
            _ => BadRequest(new { ok = false, message = "Danh sách tin nhắn không hợp lệ." })
        };
    }
}
