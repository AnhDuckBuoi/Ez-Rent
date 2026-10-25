using System.Security.Claims;
using EZ_Rent.Areas.Landlord.Services;
using EZ_Rent.Areas.Landlord.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// Lớp nền cho mọi trang chủ trọ mới:
/// - Bắt buộc đăng nhập + vai trò Landlord; tài khoản bị khóa/cấm (UserStatus) bị từ chối ngay cả khi cookie còn hạn.
/// - [AutoValidateAntiforgeryToken]: mọi POST (form thường và fetch/AJAX) phải có CSRF token.
/// - Mọi truy vấn dữ liệu đi qua các tập "My..." đã lọc theo chủ trọ đang đăng nhập; id của người khác trả 404.
/// - Ghi log (R68): mọi thao tác thành công/bị từ chối, truy cập dữ liệu không thuộc quyền, tài khoản bị khóa.
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
[AutoValidateAntiforgeryToken]
public abstract class LandlordControllerBase : Controller
{
    protected const int PageSize = 10;

    protected LandlordControllerBase(AppDbContext db, TimeProvider? clock)
    {
        Db = db;
        Clock = clock ?? TimeProvider.System;
    }

    protected AppDbContext Db { get; }
    protected TimeProvider Clock { get; }
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    protected DateTime UtcNow => Clock.GetUtcNow().UtcDateTime;
    protected DateTime VietnamNow => LandlordTime.ToVietnam(UtcNow);
    protected DateTime VietnamToday => VietnamNow.Date;

    private ILogger? log;
    /// <summary>Logger chung "EZ_Rent.Landlord" (cấu hình mức log trong appsettings như các log khác của ứng dụng).</summary>
    protected ILogger Log => log ??= HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EZ_Rent.Landlord");
    private string ActionLabel => $"{ControllerContext.ActionDescriptor.ControllerName}/{ControllerContext.ActionDescriptor.ActionName}";

    /// <summary>Yêu cầu từ landlord.js (fetch) mong nhận JSON thay vì chuyển trang.</summary>
    protected bool IsAjax => string.Equals(Request.Headers["X-Requested-With"], "fetch", StringComparison.Ordinal);

    /// <summary>Yêu cầu nạp lại một vùng của trang (danh sách, bộ lọc, phân trang) bằng AJAX.</summary>
    protected bool IsRegionRequest => Request.Headers.ContainsKey("X-EZ-Region");

    protected IQueryable<RentalPost> MyPosts => Db.RentalPosts.Where(p => p.OwnerId == CurrentUserId);
    protected IQueryable<ViewingAppointment> MyViewings => Db.ViewingAppointments.Where(a => a.RentalPost.OwnerId == CurrentUserId);
    protected IQueryable<RentalContract> MyContracts => Db.RentalContracts.Where(c => c.LandlordId == CurrentUserId);
    protected IQueryable<Invoice> MyInvoices => Db.Invoices.Where(i => i.RentalContract.LandlordId == CurrentUserId);
    protected IQueryable<MeterReading> MyReadings => Db.MeterReadings.Where(m => m.RentalPost.OwnerId == CurrentUserId);
    protected IQueryable<RepairRequest> MyRepairs => Db.RepairRequests.Where(r => r.RentalPost.OwnerId == CurrentUserId);
    protected IQueryable<Conversation> MyConversations => Db.Conversations.Where(c =>
        (c.InitiatorId == CurrentUserId || c.RecipientId == CurrentUserId) && c.InitiatorId != c.RecipientId && c.ConversationType == ConversationType.TenantLandlord);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId)) { context.Result = Forbid(); return; }
        var me = await Db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Status, u.FullName, u.AvatarUrl }).FirstOrDefaultAsync(HttpContext.RequestAborted);
        if (me == null || me.Status != UserStatus.Active)
        {
            Log.LogWarning("Chặn tài khoản chủ trọ {UserId} (trạng thái {Status}) truy cập {Path}", userId, me?.Status, Request.Path);
            context.Result = Forbid();
            return;
        }

        if (!IsAjax && !IsRegionRequest)
        {
            ViewData["LandlordNav"] = new LandlordNavCounts
            {
                Name = string.IsNullOrWhiteSpace(me.FullName) ? "Chủ trọ" : me.FullName,
                AvatarUrl = me.AvatarUrl,
                PendingViewings = await MyViewings.CountAsync(a => a.Status == ViewingAppointmentStatus.Pending),
                UnreadMessages = await MyConversations.SelectMany(c => c.Messages)
                    .CountAsync(m => !m.IsRead && m.SenderId != userId),
                OpenRepairs = await MyRepairs.CountAsync(r => r.Status == RepairRequestStatus.Pending),
                SupportReplies = await Db.Conversations
                    .Where(c => c.ConversationType == ConversationType.LandlordAdmin && c.InitiatorId == userId)
                    .SelectMany(c => c.Messages).CountAsync(m => !m.IsRead && m.SenderId != userId)
            };
        }
        await next();
    }

    protected IActionResult PageOrRegion(string view, string region, object model) =>
        IsRegionRequest ? PartialView(region, model) : View(view, model);

    /// <summary>Thao tác thành công: JSON cho AJAX, hoặc thông báo + chuyển trang (Post/Redirect/Get).</summary>
    protected IActionResult Done(string message, string redirectUrl)
    {
        Log.LogInformation("Chủ trọ {UserId} {Action} id={Id}: {Result}", CurrentUserId, ActionLabel, RouteData.Values["id"], message);
        if (IsAjax) return Json(new { ok = true, message });
        TempData[LandlordFlash.Key] = message;
        TempData[LandlordFlash.KindKey] = "ok";
        return LocalRedirect(redirectUrl);
    }

    /// <summary>Thao tác bị từ chối (dữ liệu sai, trạng thái vừa đổi...). Không ghi gì vào DB.</summary>
    protected IActionResult Fail(int statusCode, string message, string backUrl)
    {
        Log.LogWarning("Chủ trọ {UserId} {Action} id={Id} bị từ chối ({Status}): {Reason}", CurrentUserId, ActionLabel, RouteData.Values["id"], statusCode, message);
        if (IsAjax)
        {
            Response.StatusCode = statusCode;
            return Json(new { ok = false, message });
        }
        TempData[LandlordFlash.Key] = message;
        TempData[LandlordFlash.KindKey] = "error";
        return LocalRedirect(backUrl);
    }

    protected IActionResult MissingPage(string what, string backUrl)
    {
        // Có thể là dò id của người khác (IDOR): ghi lại để kiểm tra.
        Log.LogWarning("Chủ trọ {UserId} truy cập {Method} {Path}: không tồn tại hoặc không thuộc quyền", CurrentUserId, Request.Method, Request.Path);
        if (IsAjax) { Response.StatusCode = StatusCodes.Status404NotFound; return Json(new { ok = false, message = $"Không tìm thấy {what}." }); }
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("~/Areas/Landlord/Views/Shared/Problem.cshtml",
            new ProblemModel($"Không tìm thấy {what}", $"{char.ToUpper(what[0])}{what[1..]} không tồn tại hoặc không thuộc tài khoản của bạn.", backUrl));
    }

    protected IActionResult InvalidQuery(string backUrl)
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("~/Areas/Landlord/Views/Shared/Problem.cshtml",
            new ProblemModel("Bộ lọc không hợp lệ", "Đường dẫn chứa giá trị lọc hoặc số trang không hợp lệ.", backUrl, "Về danh sách"));
    }

    protected string Back(string action, object? values = null) => Url.Action(action, values) ?? "/Landlord/Overview";

    protected static string? NoteOrNull(string? text, int max) => LandlordFormat.Clean(text, max);
}
