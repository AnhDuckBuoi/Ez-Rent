using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
public class ViewingAppointmentController : TenantControllerBase
{
    public ViewingAppointmentController(AppDbContext context) : base(context) { }

    private IQueryable<ViewingAppointment> MyAppointments => Context.ViewingAppointments
        .Where(a => a.TenantId == CurrentUserId);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AppointmentListInput input)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new AppointmentListModel { Input = input });
        }
        var counts = await MyAppointments.AsNoTracking().GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() }).ToDictionaryAsync(g => g.Status, g => g.Count);
        var query = MyAppointments.AsNoTracking();
        if (input.Status.HasValue) query = query.Where(a => a.Status == input.Status.Value);
        var total = input.Status.HasValue ? counts.GetValueOrDefault(input.Status.Value) : counts.Values.Sum();
        const int pageSize = 10;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        input.Page = Math.Min(input.Page, pages);
        // Active requests first, earliest viewing first; history follows with the newest viewing first.
        var rows = await query.Include(a => a.RentalPost)
            .OrderBy(a => a.Status == ViewingAppointmentStatus.Pending || a.Status == ViewingAppointmentStatus.Confirmed ? 0 : 1)
            .ThenBy(a => a.Status == ViewingAppointmentStatus.Pending || a.Status == ViewingAppointmentStatus.Confirmed ? a.ViewingDate : DateTime.MaxValue)
            .ThenByDescending(a => a.ViewingDate).ThenByDescending(a => a.ViewingAppointmentId)
            .Skip((input.Page - 1) * pageSize).Take(pageSize).ToListAsync();
        return View(new AppointmentListModel
        {
            Input = input, Counts = counts, FilteredTotal = total, TotalPages = pages,
            Items = rows.Select(AppointmentModel.From).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int? id)
    {
        var appointment = await MyAppointments.AsNoTracking().Include(a => a.RentalPost)
            .FirstOrDefaultAsync(a => a.ViewingAppointmentId == id);
        return appointment == null ? MissingAppointment() : View(AppointmentModel.From(appointment));
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulatePostsAsync();
        return View(new ViewingAppointmentInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ViewingAppointmentInputModel input)
    {
        await ValidatePostAsync(input.RentalPostId);
        if (ModelState.IsValid)
        {
            Context.ViewingAppointments.Add(new ViewingAppointment
            {
                TenantId = CurrentUserId,
                RentalPostId = input.RentalPostId,
                ViewingDate = input.ViewingDate!.Value,
                Note = input.Note,
                Status = ViewingAppointmentStatus.Pending
            });
            await Context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        await PopulatePostsAsync(input.RentalPostId);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] int? id)
    {
        var appointment = await MyAppointments.AsNoTracking().FirstOrDefaultAsync(a => a.ViewingAppointmentId == id);
        if (appointment == null) return NotFound();
        if (appointment.Status != ViewingAppointmentStatus.Pending) return Conflict();
        await PopulatePostsAsync(appointment.RentalPostId);
        return View(new ViewingAppointmentInputModel
        {
            RentalPostId = appointment.RentalPostId, ViewingDate = appointment.ViewingDate, Note = appointment.Note
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, ViewingAppointmentInputModel input)
    {
        var appointment = await MyAppointments.FirstOrDefaultAsync(a => a.ViewingAppointmentId == id);
        if (appointment == null) return NotFound();
        if (appointment.Status != ViewingAppointmentStatus.Pending) return Conflict();
        await ValidatePostAsync(input.RentalPostId);
        if (ModelState.IsValid)
        {
            // The status predicate also protects against a concurrent Landlord confirmation.
            var changed = await MyAppointments.Where(a => a.ViewingAppointmentId == id && a.Status == ViewingAppointmentStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.RentalPostId, input.RentalPostId)
                    .SetProperty(a => a.ViewingDate, input.ViewingDate!.Value)
                    .SetProperty(a => a.Note, input.Note).SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
            return changed == 0 ? Conflict() : RedirectToAction(nameof(Index));
        }
        await PopulatePostsAsync(input.RentalPostId);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Delete([FromRoute] int? id)
    {
        var appointment = await MyAppointments.AsNoTracking().Include(a => a.RentalPost)
            .FirstOrDefaultAsync(a => a.ViewingAppointmentId == id);
        if (appointment == null) return MissingAppointment();
        return CanCancel(appointment.Status) ? View(AppointmentModel.From(appointment)) : CancellationConflict(appointment.ViewingAppointmentId);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id)
    {
        var appointment = await MyAppointments.AsNoTracking().FirstOrDefaultAsync(a => a.ViewingAppointmentId == id);
        if (appointment == null) return MissingAppointment();
        if (!CanCancel(appointment.Status)) return CancellationConflict(id);
        // Keep the history; never delete a Landlord's response with the appointment.
        var changed = await MyAppointments.Where(a => a.ViewingAppointmentId == id &&
                (a.Status == ViewingAppointmentStatus.Pending || a.Status == ViewingAppointmentStatus.Confirmed))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Status, ViewingAppointmentStatus.Cancelled)
                .SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
        if (changed == 0) return CancellationConflict(id);
        TempData["DiscoveryMessage"] = "Đã hủy lịch hẹn. Lịch sử và phản hồi của chủ nhà vẫn được giữ lại.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private IActionResult MissingAppointment()
    {
        Response.StatusCode = StatusCodes.Status404NotFound;
        return View("Unavailable");
    }

    private IActionResult CancellationConflict(int id)
    {
        Response.StatusCode = StatusCodes.Status409Conflict;
        ViewData["AppointmentId"] = id;
        return View("CancellationConflict");
    }

    private static bool CanCancel(ViewingAppointmentStatus status) =>
        status is ViewingAppointmentStatus.Pending or ViewingAppointmentStatus.Confirmed;

    private async Task ValidatePostAsync(int postId)
    {
        if (!await PublicAvailablePosts.AnyAsync(p => p.RentalPostId == postId))
            ModelState.AddModelError(nameof(ViewingAppointmentInputModel.RentalPostId), "Phòng không còn được công khai hoặc không còn trống.");
    }

    private async Task PopulatePostsAsync(int? selectedId = null) => ViewData["RentalPostId"] =
        new SelectList(await PublicAvailablePosts.AsNoTracking().OrderBy(p => p.Title)
            .Select(p => new { p.RentalPostId, p.Title }).ToListAsync(), "RentalPostId", "Title", selectedId);
}
