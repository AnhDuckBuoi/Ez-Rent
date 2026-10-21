using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
public sealed class ViewingBookingController(AppDbContext context, ITenantViewingScheduleReader? schedule = null, TimeProvider? clock = null)
    : TenantControllerBase(context)
{
    [HttpGet]
    public async Task<IActionResult> Index([FromRoute] int id, [FromQuery] DateOnly? date, string? returnUrl)
    {
        var detail = await TenantRentalDiscovery.DetailsAsync(Context, id, CurrentUserId);
        if (detail == null) { Response.StatusCode = 404; return View("~/Areas/Tenant/Views/RentalPost/Unavailable.cshtml"); }
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var today = DateOnly.FromDateTime(now.ToOffset(TimeSpan.FromHours(7)).DateTime);
        if (!ModelState.IsValid || date < today)
        {
            Response.StatusCode = 400;
            ViewData["PostId"] = id;
            return View("InvalidDate");
        }
        var day = date ?? today;
        // Back always returns to this same room; the listing return URL remains nested inside it.
        var listing = Url.IsLocalUrl(returnUrl) ? returnUrl : Url.Action("Index", "RentalPost");
        detail.BackUrl = Url.Action("Details", "RentalPost", new { id, returnUrl = listing })!;
        ViewData["ListingReturnUrl"] = listing;
        var source = new TenantViewingSchedule(TenantScheduleState.NotConnected, []);
        if (schedule != null)
        {
            try { source = await schedule.ReadAsync(id, CurrentUserId, day, HttpContext.RequestAborted); }
            catch (HttpRequestException) { source = new(TenantScheduleState.Error, []); }
            catch (TimeoutException) { source = new(TenantScheduleState.Error, []); }
        }
        // Enforce display boundaries even if a future reader supplies stale or out-of-day rows.
        // These are read/display rules only, not the server validation required for submitting a booking.
        var slots = source.State is TenantScheduleState.Open or TenantScheduleState.Full
            ? source.Slots.Where(s => !string.IsNullOrWhiteSpace(s.Id) && s.ArrivalAt >= now.AddHours(6)
                && DateOnly.FromDateTime(s.ArrivalAt.ToOffset(TimeSpan.FromHours(7)).DateTime) == day)
                .DistinctBy(s => s.Id).OrderBy(s => s.ArrivalAt).ToList() : [];
        var state = source.State == TenantScheduleState.Open && !slots.Any(s => s.State == TenantSlotState.Available)
            ? TenantScheduleState.Full : source.State;
        return View(new ViewingBookingModel { Detail = detail, Today = today, Date = day, Slots = slots, State = state, IsDemo = source.IsDemo });
    }
}
