using System.Data.Common;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant"), Authorize(Roles = "Tenant")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class SupportController(AppDbContext context, ILogger<SupportController> logger, EZ_Rent.Services.ChatRealtime realtime) : TenantControllerBase(context)
{
    [HttpGet]
    public async Task<IActionResult> Create([FromQuery] int? rentalPostId, CancellationToken ct)
    {
        if (!ModelState.IsValid || rentalPostId is < 1) return BadRequest();
        var input = new TenantSupportInput { RentalPostId = rentalPostId };
        try
        {
            if (rentalPostId.HasValue && !await TenantSupportService.RelatedRooms(Context, CurrentUserId).AnyAsync(p => p.RentalPostId == rentalPostId, ct))
                return NotFound();
            return View(await TenantSupportService.ReadAsync(Context, CurrentUserId, input, ct));
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "Không mở được biểu mẫu hỗ trợ Tenant.");
            return await Failure(input, 503, "Chưa tải được nơi tiếp nhận. Hãy thử lại sau; bạn vẫn có thể sao chép nội dung đã soạn.", ct);
        }
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(16384)]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] TenantSupportInput input, CancellationToken ct)
    {
        if (!ModelState.IsValid) return await Failure(input, 400, "Kiểm tra các trường được đánh dấu trước khi gửi.", ct);
        try
        {
            var result = await TenantSupportService.CreateAsync(Context, CurrentUserId, input, ct);
            if (result.Status != MessageWriteStatus.Saved)
            {
                var (status, error) = result.Status switch
                {
                    MessageWriteStatus.Missing => (409, "Phòng liên quan không còn khả dụng. Bỏ chọn phòng hoặc mở lại biểu mẫu; nội dung vẫn được giữ."),
                    MessageWriteStatus.ReadOnly => (409, "Hiện chưa thể tiếp nhận yêu cầu. Kiểm tra tài khoản hoặc thử lại sau; nội dung vẫn được giữ."),
                    _ => (400, "Kiểm tra vấn đề, tiêu đề và mô tả trước khi gửi.")
                };
                return await Failure(input, status, error, ct);
            }
            await realtime.ChangedAsync(result.ConversationId);
            var url = Url.Action("Messages", "Dashboard", new { ConversationId = result.ConversationId, Filter = "support", View = "chat" })!;
            TempData["DiscoveryMessage"] = "Đã gửi yêu cầu hỗ trợ. Bạn có thể theo dõi và bổ sung nội dung tại đây.";
            if (AjaxRequest) return Json(new { url });
            return LocalRedirect(url);
        }
        catch (Exception ex) when (ex is DbException or DbUpdateException)
        {
            logger.LogError(ex, "Không hoàn tất gửi yêu cầu hỗ trợ Tenant.");
            return await Failure(input, 503, "Chưa xác nhận được kết quả gửi. Hãy xem Yêu cầu đã gửi trước khi gửi lại; nội dung vẫn được giữ.", ct);
        }
    }

    private bool AjaxRequest => Request.Headers["X-Requested-With"] == "XMLHttpRequest";

    private async Task<IActionResult> Failure(TenantSupportInput input, int status, string error, CancellationToken ct)
    {
        if (AjaxRequest) return StatusCode(status, new { error, errors = ModelState.Where(p => p.Value?.Errors.Count > 0)
            .ToDictionary(p => p.Key, p => p.Value!.Errors.Select(e => string.IsNullOrEmpty(e.ErrorMessage) ? "Giá trị không hợp lệ." : e.ErrorMessage).ToArray()) });
        TenantSupportModel model;
        try { model = await TenantSupportService.ReadAsync(Context, CurrentUserId, input, ct); }
        catch (Exception ex) when (ex is DbException or DbUpdateException) { model = new TenantSupportModel { Input = input }; }
        model.Error = error;
        Response.StatusCode = status;
        return View("Create", model);
    }
}
