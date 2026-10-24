using System.Security.Claims;
using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Data;
using EZ_Rent.Helpers;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// Lớp nền cho các trang chủ trọ, theo cùng quy ước với khu Admin của main:
/// - [Area("Landlord")] + [Authorize(Roles = "Landlord")] (mỗi controller con cũng ghi lại hai attribute này cho dễ đọc);
///   tài khoản bị khóa/cấm (UserStatus) bị chặn ngay cả khi cookie còn hạn.
/// - [AutoValidateAntiforgeryToken]: mọi POST, kể cả fetch/AJAX, phải có CSRF token.
/// - Mọi truy vấn đi qua các tập "My..." đã lọc theo chủ trọ đang đăng nhập; id của người khác trả 404.
/// - JSON trả về dạng ApiResult (success/message) như Admin; phân trang bằng PaginatedList + partial _Pagination của main.
/// - Ghi log thao tác thành công/bị từ chối và truy cập id không thuộc quyền (R68).
/// Không trùng LandlordControllerBase có sẵn (lớp đó phục vụ MessagesController của nhóm).
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
[AutoValidateAntiforgeryToken]
public abstract class LandlordAreaController : Controller
{
    protected const int PageSize = 10;
    public const string StatusKey = "LandlordStatus";
    public const string StatusTypeKey = "LandlordStatusType";

    protected LandlordAreaController(AppDbContext db, TimeProvider? clock)
    {
        Db = db;
        Clock = clock ?? TimeProvider.System;
    }

    protected AppDbContext Db { get; }
    protected TimeProvider Clock { get; }
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
    protected DateTime UtcNow => Clock.GetUtcNow().UtcDateTime;
    protected DateTime VietnamNow => VietnamTime.ToVietnam(UtcNow);
    protected DateTime VietnamToday => VietnamNow.Date;

    private ILogger? log;
    protected ILogger Log => log ??= HttpContext.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EZ_Rent.Landlord");
    private string ActionLabel => $"{ControllerContext.ActionDescriptor.ControllerName}/{ControllerContext.ActionDescriptor.ActionName}";

    /// <summary>Yêu cầu fetch từ landlord.js (header chuẩn X-Requested-With) mong nhận JSON.</summary>
    protected bool IsAjax => string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.Ordinal);

    /// <summary>Yêu cầu nạp lại riêng vùng danh sách (lọc, đổi tab, chuyển trang) bằng AJAX.</summary>
    protected bool IsRegionRequest => Request.Headers.ContainsKey("X-EZ-Region");

    protected IQueryable<RentalPost> MyPosts => Db.RentalPosts.Where(p => p.OwnerId == CurrentUserId);
    protected IQueryable<ViewingAppointment> MyViewings => Db.ViewingAppointments.Where(a => a.RentalPost.OwnerId == CurrentUserId);
    protected IQueryable<RentalContract> MyContracts => Db.RentalContracts.Where(c => c.LandlordId == CurrentUserId);
    protected IQueryable<Invoice> MyInvoices => Db.Invoices.Where(i => i.RentalContract.LandlordId == CurrentUserId);
    protected IQueryable<MeterReading> MyReadings => Db.MeterReadings.Where(m => m.RentalPost.OwnerId == CurrentUserId);
    protected IQueryable<RepairRequest> MyRepairs => Db.RepairRequests.Where(r => r.RentalPost.OwnerId == CurrentUserId);

    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var userId = CurrentUserId;
        if (string.IsNullOrWhiteSpace(userId)) { context.Result = Forbid(); return; }
        var me = await Db.Users.AsNoTracking().Where(u => u.Id == userId)
            .Select(u => new { u.Status, u.FullName }).FirstOrDefaultAsync(HttpContext.RequestAborted);
        if (me == null || me.Status != UserStatus.Active)
        {
            Log.LogWarning("Chặn tài khoản chủ trọ {UserId} (trạng thái {Status}) truy cập {Path}", userId, me?.Status, Request.Path);
            context.Result = Forbid();
            return;
        }
        // SEO đúng cho khu quản lý riêng tư: không cho công cụ tìm kiếm lập chỉ mục. Dùng header vì _Layout chung
        // của main không có chỗ chèn thẻ meta riêng (trang đăng ký công khai /Landlord/Join vẫn được index).
        Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
        if (!IsAjax && !IsRegionRequest)
        {
            ViewData[LandlordMenuCounts.Key] = new LandlordMenuCounts
            {
                Name = string.IsNullOrWhiteSpace(me.FullName) ? "Chủ trọ" : me.FullName,
                PendingViewings = await MyViewings.CountAsync(a => a.Status == ViewingAppointmentStatus.Pending),
                UnreadMessages = await Db.Conversations
                    .Where(c => (c.InitiatorId == userId || c.RecipientId == userId) && c.ConversationType == ConversationType.TenantLandlord)
                    .SelectMany(c => c.Messages).CountAsync(m => !m.IsRead && m.SenderId != userId),
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

    /// <summary>Đếm, kẹp số trang vào khoảng hợp lệ rồi lấy đúng một trang (phân trang số như Admin).</summary>
    protected async Task<PaginatedList<T>> PageAsync<T>(IQueryable<T> query, int page)
    {
        var count = await query.CountAsync();
        var pages = Math.Max(1, (int)Math.Ceiling(count / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        var items = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return new PaginatedList<T>(items, count, page, PageSize);
    }

    /// <summary>Thông tin cho partial _Pagination của main; giữ nguyên bộ lọc hiện tại trên query string.</summary>
    protected PaginationInfo Pager<T>(PaginatedList<T> list, string action = "Index", object? routeValues = null)
    {
        var values = Request.Query.Where(q => !string.Equals(q.Key, "page", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(q => q.Key, q => (string?)q.Value.ToString());
        foreach (var (key, value) in new Microsoft.AspNetCore.Routing.RouteValueDictionary(routeValues))
            values[key] = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture);
        return PaginationInfo.From(list, action, ControllerContext.ActionDescriptor.ControllerName, "Landlord", values);
    }

    /// <summary>Thành công: JSON ApiResult cho AJAX, hoặc thông báo + chuyển trang (Post/Redirect/Get).</summary>
    protected IActionResult Done(string message, string redirectUrl)
    {
        Log.LogInformation("Chủ trọ {UserId} {Action} id={Id}: {Result}", CurrentUserId, ActionLabel, RouteData.Values["id"], message);
        if (IsAjax) return Json(ApiResult.Ok(message));
        TempData[StatusKey] = message;
        TempData[StatusTypeKey] = "success";
        return LocalRedirect(redirectUrl);
    }

    /// <summary>Bị từ chối (dữ liệu sai, trạng thái vừa đổi...). Không ghi gì vào DB.</summary>
    protected IActionResult Fail(int statusCode, string message, string backUrl)
    {
        Log.LogWarning("Chủ trọ {UserId} {Action} id={Id} bị từ chối ({Status}): {Reason}", CurrentUserId, ActionLabel, RouteData.Values["id"], statusCode, message);
        if (IsAjax)
        {
            Response.StatusCode = statusCode;
            return Json(ApiResult.Fail(message));
        }
        TempData[StatusKey] = message;
        TempData[StatusTypeKey] = "danger";
        return LocalRedirect(backUrl);
    }

    protected IActionResult NotFoundPage(string what, string backUrl)
    {
        // Có thể là dò id của người khác (IDOR): ghi lại để kiểm tra.
        Log.LogWarning("Chủ trọ {UserId} truy cập {Method} {Path}: không tồn tại hoặc không thuộc quyền", CurrentUserId, Request.Method, Request.Path);
        Response.StatusCode = StatusCodes.Status404NotFound;
        if (IsAjax) return Json(ApiResult.Fail($"Không tìm thấy {what}."));
        return View("~/Areas/Landlord/Views/Shared/LandlordProblem.cshtml",
            new LandlordProblem($"Không tìm thấy {what}", $"{char.ToUpper(what[0])}{what[1..]} không tồn tại hoặc không thuộc tài khoản của bạn.", backUrl));
    }

    protected IActionResult InvalidQuery(string backUrl)
    {
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View("~/Areas/Landlord/Views/Shared/LandlordProblem.cshtml",
            new LandlordProblem("Bộ lọc không hợp lệ", "Đường dẫn chứa giá trị lọc hoặc số trang không hợp lệ.", backUrl, "Về danh sách"));
    }

    protected string Back(string action, object? values = null) => Url.Action(action, values) ?? "/Landlord/Overview";
    protected static string? NoteOrNull(string? text, int max) => DisplayFormat.Clean(text, max);
}
