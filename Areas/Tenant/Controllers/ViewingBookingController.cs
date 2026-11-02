using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
<<<<<<< HEAD
=======
using Microsoft.EntityFrameworkCore;
>>>>>>> feature/ez-rent-update

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
<<<<<<< HEAD
=======

    /// <summary>
    /// Gửi yêu cầu xem phòng theo khung giờ chủ trọ đã mở (quyết định 15/10 của nhóm). Kiểm tra lại ở server:
    /// phòng còn hiển thị, giờ còn trong lịch và đang trống, cách hiện tại ít nhất 6 giờ, mỗi phòng chỉ một yêu cầu đang chờ.
    /// Lịch tạo ra ở trạng thái "Chờ xác nhận"; chủ trọ xác nhận hoặc từ chối ở trang Lịch xem phòng.
    /// </summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromRoute] int id, [FromForm] DateOnly date, [FromForm(Name = "SlotId")] string? slotId,
        [FromForm] string? note)
    {
        var back = Url.Action(nameof(Index), new { id, date = date.ToString("yyyy-MM-dd") })!;
        if (await TenantRentalDiscovery.DetailsAsync(Context, id, CurrentUserId) == null) return NotFound();
        if (schedule == null || string.IsNullOrWhiteSpace(slotId))
        {
            TempData["DiscoveryMessage"] = "Hãy chọn một giờ đến xem trước khi gửi yêu cầu.";
            return LocalRedirect(back);
        }
        var now = (clock ?? TimeProvider.System).GetUtcNow();
        var source = await schedule.ReadAsync(id, CurrentUserId, date, HttpContext.RequestAborted);
        var slot = source.Slots.FirstOrDefault(s => s.Id == slotId);
        if (source.State != TenantScheduleState.Open || slot == null || slot.State != TenantSlotState.Available || slot.ArrivalAt < now.AddHours(6))
        {
            TempData["DiscoveryMessage"] = "Giờ bạn chọn không còn trống hoặc đã quá sát giờ (cần đặt trước ít nhất 6 giờ). Hãy chọn giờ khác.";
            return LocalRedirect(back);
        }
        if (await Context.ViewingAppointments.AnyAsync(a => a.RentalPostId == id && a.TenantId == CurrentUserId &&
                a.Status == EZ_Rent.Models.Enums.ViewingAppointmentStatus.Pending))
        {
            TempData["DiscoveryMessage"] = "Bạn đã có một yêu cầu đang chờ chủ nhà xác nhận cho phòng này.";
            return LocalRedirect(back);
        }
        var trimmed = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (trimmed?.Length > 500) trimmed = trimmed[..500];
        var appointment = new EZ_Rent.Models.ViewingAppointment
        {
            RentalPostId = id, TenantId = CurrentUserId, ViewingDate = slot.ArrivalAt.ToOffset(TimeSpan.FromHours(7)).DateTime,
            Note = trimmed, Status = EZ_Rent.Models.Enums.ViewingAppointmentStatus.Pending, CreatedAt = now.UtcDateTime
        };
        Context.ViewingAppointments.Add(appointment);
        await Context.SaveChangesAsync();
        TempData["DiscoveryMessage"] = "Đã gửi yêu cầu xem phòng. Đang chờ chủ nhà xác nhận — vui lòng chưa đến xem phòng.";
        return RedirectToAction("Details", "ViewingAppointment", new { area = "Tenant", id = appointment.ViewingAppointmentId });
    }
>>>>>>> feature/ez-rent-update
}
