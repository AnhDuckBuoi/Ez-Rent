using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class RepairRequestController : TenantControllerBase
{
    public RepairRequestController(AppDbContext context) : base(context) { }

    private IQueryable<RepairRequest> MyRequests => Context.RepairRequests.Where(r => r.TenantId == CurrentUserId);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] RepairListInput input)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new RepairListModel(input, [], 0, 1));
        }
        if (input.RoomId.HasValue && !await Context.RentalContracts.AnyAsync(c =>
            c.RentalPostId == input.RoomId && c.TenantId == CurrentUserId)) return NotFound();
        var query = MyRequests.AsNoTracking();
        if (input.RoomId.HasValue) query = query.Where(r => r.RentalPostId == input.RoomId);
        if (input.Status.HasValue) query = query.Where(r => r.Status == input.Status);
        var total = await query.CountAsync();
        var pages = TenantRentalPresentation.Pages(total);
        input.Page = Math.Min(input.Page, pages);
        var items = await query.Include(r => r.RentalPost).OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.RepairRequestId)
            .Skip((input.Page - 1) * TenantRentalPresentation.PageSize).Take(TenantRentalPresentation.PageSize).ToListAsync();
        return View(new RepairListModel(input, items, total, pages));
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int? id)
    {
        var request = await MyRequests.AsNoTracking().Include(r => r.RentalPost).Include(r => r.Tenant)
            .FirstOrDefaultAsync(r => r.RepairRequestId == id);
        return request == null ? NotFound() : View(request);
    }

    [HttpGet]
    public async Task<IActionResult> Create([FromQuery] int? roomId)
    {
        if (!ModelState.IsValid || roomId is <= 0) return BadRequest();
        if (roomId.HasValue && !await MyRentedPosts.AnyAsync(p => p.RentalPostId == roomId)) return NotFound();
        await PopulatePostsAsync(roomId);
        return View(new RepairRequestInputModel { RentalPostId = roomId ?? 0 });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(RepairRequestInputModel input)
    {
        await ValidatePostAsync(input.RentalPostId);
        if (!string.IsNullOrWhiteSpace(input.ImageUrl) && RentalPresentation.SafeMediaUrl(input.ImageUrl) == null)
            ModelState.AddModelError(nameof(input.ImageUrl), "Dùng liên kết ảnh HTTP hoặc HTTPS hợp lệ.");
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
            TempData["DiscoveryMessage"] = "Đã gửi yêu cầu sửa chữa.";
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
        if (request.Status != RepairRequestStatus.Pending) return RequestChanged();
        await PopulatePostsAsync(request.RentalPostId);
        return View(new RepairRequestInputModel
        {
            RentalPostId = request.RentalPostId,
            ProblemType = request.ProblemType,
            Description = request.Description,
            Priority = request.Priority,
            ImageUrl = request.ImageUrl
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, RepairRequestInputModel input)
    {
        var request = await MyRequests.AsNoTracking().FirstOrDefaultAsync(r => r.RepairRequestId == id);
        if (request == null) return NotFound();
        if (request.Status != RepairRequestStatus.Pending) return RequestChanged();
        if (input.RentalPostId != request.RentalPostId)
            ModelState.AddModelError(nameof(input.RentalPostId), "Không thể đổi phòng của yêu cầu đã gửi.");
        await ValidatePostAsync(input.RentalPostId);
        if (!string.IsNullOrWhiteSpace(input.ImageUrl) && RentalPresentation.SafeMediaUrl(input.ImageUrl) == null)
            ModelState.AddModelError(nameof(input.ImageUrl), "Dùng liên kết ảnh HTTP hoặc HTTPS hợp lệ.");
        if (ModelState.IsValid)
        {
            var changed = await MyRequests.Where(r => r.RepairRequestId == id && r.Status == RepairRequestStatus.Pending)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.ProblemType, input.ProblemType)
                    .SetProperty(r => r.Description, input.Description).SetProperty(r => r.Priority, input.Priority)
                    .SetProperty(r => r.ImageUrl, input.ImageUrl).SetProperty(r => r.UpdatedAt, DateTime.UtcNow));
            if (changed == 0) return RequestChanged();
            TempData["DiscoveryMessage"] = "Đã cập nhật yêu cầu sửa chữa.";
            return RedirectToAction(nameof(Index));
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
        return request.Status == RepairRequestStatus.Pending ? View(request) : RequestChanged();
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id)
    {
        var request = await MyRequests.AsNoTracking().FirstOrDefaultAsync(r => r.RepairRequestId == id);
        if (request == null) return NotFound();
        if (request.Status != RepairRequestStatus.Pending) return RequestChanged();
        var changed = await MyRequests.Where(r => r.RepairRequestId == id && r.Status == RepairRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, RepairRequestStatus.Cancelled)
                .SetProperty(r => r.UpdatedAt, DateTime.UtcNow));
        if (changed == 0) return RequestChanged();
        TempData["DiscoveryMessage"] = "Đã hủy yêu cầu. Lịch sử được giữ lại để bạn tra cứu.";
        return RedirectToAction(nameof(Index));
    }

    private ViewResult RequestChanged()
    {
        Response.StatusCode = StatusCodes.Status409Conflict;
        return View("RentalConflict");
    }

    private async Task ValidatePostAsync(int postId)
    {
        if (!await MyRentedPosts.AnyAsync(p => p.RentalPostId == postId))
            ModelState.AddModelError(nameof(RepairRequestInputModel.RentalPostId), "Phòng phải thuộc hợp đồng đang hiệu lực của bạn.");
    }

    private async Task PopulatePostsAsync(int? selectedId = null)
    {
        var posts = await MyRentedPosts.AsNoTracking().OrderBy(p => p.Title)
            .Select(p => new { p.RentalPostId, p.Title }).ToListAsync();
        ViewData["HasRentedPosts"] = posts.Count > 0;
        ViewData["RentalPostId"] = new SelectList(posts, "RentalPostId", "Title", selectedId);
    }
}
