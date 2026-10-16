using System.Security.Claims;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.ViewComponents;

public sealed class TenantDesktopHeaderViewComponent(AppDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var userId = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = string.IsNullOrWhiteSpace(userId) ? 0
            : await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
        return View("~/Areas/Tenant/Views/Shared/_TenantDesktopHeader.cshtml", new TenantHeaderModel(
            RouteData.Values["controller"]?.ToString() is "ViewingBooking" ? "RentalPost" : RouteData.Values["controller"]?.ToString() ?? "",
            RouteData.Values["action"]?.ToString() ?? "", unread));
    }
}
