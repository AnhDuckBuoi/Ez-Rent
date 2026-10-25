using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
public sealed class NotificationController(AppDbContext context) : TenantControllerBase(context)
{
    private IQueryable<Notification> OwnNotices => Context.Notifications.Where(n => n.UserId == CurrentUserId);

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var invalid = !ModelState.IsValid || page < 1;
        var total = await OwnNotices.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (total - 1) / 10 + 1));
        var unread = await OwnNotices.CountAsync(n => !n.IsRead);
        ViewData["UnreadNotifications"] = unread;
        var items = invalid ? [] : await OwnNotices.AsNoTracking().OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId)
            .Skip((page - 1) * 10).Take(10).ToListAsync();
        if (invalid) Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(new TenantNoticeListModel { Items = items.Select(TenantNoticeModel.From).ToList(), Total = total, UnreadCount = unread, Page = page, InvalidPage = invalid });
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var notice = await OwnNotices.AsNoTracking().SingleOrDefaultAsync(n => n.NotificationId == id);
        if (notice == null) return NotFound();
        var model = TenantNoticeModel.From(notice);
        var target = TenantNotificationTarget.Parse(notice.Link);
        model.CanOpenTarget = target != null && await target.CanAccessAsync(Context, CurrentUserId);
        ViewData["UnreadNotifications"] = await OwnNotices.CountAsync(n => !n.IsRead);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Open([FromRoute] int id)
    {
        var notice = await OwnNotices.AsNoTracking().SingleOrDefaultAsync(n => n.NotificationId == id);
        if (notice == null) return NotFound();
        await OwnNotices.Where(n => n.NotificationId == id && !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        var target = TenantNotificationTarget.Parse(notice.Link);
        if (target != null && await target.CanAccessAsync(Context, CurrentUserId))
            return RedirectToAction("Details", target.Controller, new { area = "Tenant", id = target.Id });
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReadAll(bool returnHome = false)
    {
        await OwnNotices.Where(n => !n.IsRead).ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
        TempData["DiscoveryMessage"] = "Đã đánh dấu các thông báo hiện có là đã đọc.";
        return returnHome ? RedirectToAction("Index", "Dashboard") : RedirectToAction(nameof(Index));
    }
}
