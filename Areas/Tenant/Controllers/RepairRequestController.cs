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
public class RepairRequestController : TenantControllerBase
{
    public RepairRequestController(AppDbContext context) : base(context) { }

    private IQueryable<RepairRequest> MyRequests => Context.RepairRequests.Where(r => r.TenantId == CurrentUserId);

    [HttpGet]
    public async Task<IActionResult> Index() => View(await MyRequests.AsNoTracking()
        .Include(r => r.RentalPost).Include(r => r.Tenant).ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int? id)
    {
        var request = await MyRequests.AsNoTracking().Include(r => r.RentalPost).Include(r => r.Tenant)
            .FirstOrDefaultAsync(r => r.RepairRequestId == id);
        return request == null ? NotFound() : View(request);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulatePostsAsync();
        return View(new RepairRequestInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RepairRequestInputModel input)
    {
        await ValidatePostAsync(input.RentalPostId);
        if (ModelState.IsValid)
        {
            Context.RepairRequests.Add(new RepairRequest
            {
                TenantId = CurrentUserId,
                RentalPostId = input.RentalPostId,
                ProblemType = input.ProblemType,
                Description = input.Description,
                Priority = input.Priority,
                ImageUrl = input.ImageUrl,
                Status = RepairRequestStatus.Pending
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
        var request = await MyRequests.AsNoTracking().FirstOrDefaultAsync(r => r.RepairRequestId == id);
        if (request == null) return NotFound();
        if (request.Status != RepairRequestStatus.Pending) return Conflict();
        await PopulatePostsAsync(request.RentalPostId);
        return View(new RepairRequestInputModel
        {
            RentalPostId = request.RentalPostId, ProblemType = request.ProblemType,
            Description = request.Description, Priority = request.Priority, ImageUrl = request.ImageUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, RepairRequestInputModel input)
    {
        var request = await MyRequests.AsNoTracking().FirstOrDefaultAsync(r => r.RepairRequestId == id);
        if (request == null) return NotFound();
        if (request.Status != RepairRequestStatus.Pending) return Conflict();
        if (input.RentalPostId != request.RentalPostId)
            ModelState.AddModelError(nameof(input.RentalPostId), "Không thể đổi phòng của yêu cầu đã gửi.");
        await ValidatePostAsync(input.RentalPostId);
        if (ModelState.IsValid)
        {
            var changed = await MyRequests.Where(r => r.RepairRequestId == id && r.Status == RepairRequestStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ProblemType, input.ProblemType)
                    .SetProperty(r => r.Description, input.Description).SetProperty(r => r.Priority, input.Priority)
                    .SetProperty(r => r.ImageUrl, input.ImageUrl).SetProperty(r => r.UpdatedAt, DateTime.UtcNow));
            return changed == 0 ? Conflict() : RedirectToAction(nameof(Index));
        }
        await PopulatePostsAsync(input.RentalPostId);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Delete([FromRoute] int? id)
    {
        var request = await MyRequests.AsNoTracking().Include(r => r.RentalPost).Include(r => r.Tenant)
            .FirstOrDefaultAsync(r => r.RepairRequestId == id);
        if (request == null) return NotFound();
        return request.Status == RepairRequestStatus.Pending ? View(request) : Conflict();
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id)
    {
        var request = await MyRequests.AsNoTracking().FirstOrDefaultAsync(r => r.RepairRequestId == id);
        if (request == null) return NotFound();
        if (request.Status != RepairRequestStatus.Pending) return Conflict();
        var changed = await MyRequests.Where(r => r.RepairRequestId == id && r.Status == RepairRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RepairRequestStatus.Cancelled)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));
        return changed == 0 ? Conflict() : RedirectToAction(nameof(Index));
    }

    private async Task ValidatePostAsync(int postId)
    {
        if (!await MyRentedPosts.AnyAsync(p => p.RentalPostId == postId))
            ModelState.AddModelError(nameof(RepairRequestInputModel.RentalPostId), "Phòng phải thuộc hợp đồng đang hiệu lực của bạn.");
    }

    private async Task PopulatePostsAsync(int? selectedId = null) => ViewData["RentalPostId"] =
        new SelectList(await MyRentedPosts.AsNoTracking().OrderBy(p => p.Title)
            .Select(p => new { p.RentalPostId, p.Title }).ToListAsync(), "RentalPostId", "Title", selectedId);
}
