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
}
