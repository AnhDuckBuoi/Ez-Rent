using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
public class RentalPostController : TenantControllerBase
{
    private readonly EZ_Rent.Services.RecommendationService recommender;
    private readonly ILogger<RentalPostController> logger;
    public RentalPostController(AppDbContext context, EZ_Rent.Services.RecommendationService recommender, ILogger<RentalPostController> logger) : base(context) { this.recommender = recommender; this.logger = logger; }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] RentalSearchInput input)
    {
        input.Normalize();
        // Binding errors (for example "abc" in a number field) should be readable in Vietnamese too.
        foreach (var (key, value, message) in new (string, object?, string)[]
        {
            (nameof(input.MinPrice), input.MinPrice, "Giá tối thiểu phải là một số hợp lệ."),
            (nameof(input.MaxPrice), input.MaxPrice, "Giá tối đa phải là một số hợp lệ."),
            (nameof(input.MinArea), input.MinArea, "Diện tích tối thiểu phải là một số hợp lệ."),
            (nameof(input.MaxArea), input.MaxArea, "Diện tích tối đa phải là một số hợp lệ."),
            (nameof(input.RoomType), input.RoomType, "Loại phòng không hợp lệ.")
        })
        {
            if (value == null && ModelState.TryGetValue(key, out var entry) && entry.Errors.Count > 0)
            {
                entry.Errors.Clear();
                ModelState.AddModelError(key, message);
            }
        }
        if (ModelState.TryGetValue(nameof(input.Page), out var pageEntry) && pageEntry.Errors.Count > 0)
        {
            pageEntry.Errors.Clear();
            ModelState.AddModelError(nameof(input.Page), "Số trang phải là số nguyên dương.");
        }
        var model = await TenantRentalDiscovery.SearchAsync(Context, input, CurrentUserId, ModelState.IsValid);
        if (ModelState.IsValid) {
            try {
                if (input.Page == 1) await EZ_Rent.Services.SearchHistoryRecorder.RecordAsync(Context, recommender, CurrentUserId,
                    input.City, input.District, input.MinPrice, input.MaxPrice, input.RoomType, HttpContext.RequestAborted);
                var recommended = await recommender.GetRecommendationsAsync(CurrentUserId, 12);
                var savedIds = await Context.Favorites.Where(f => f.UserId == CurrentUserId).Select(f => f.RentalPostId).ToListAsync();
                var recommendedIds = recommended.Select(p => p.RentalPostId).ToArray();
                var eligibleIds = await EZ_Rent.Services.Discovery.RentalSearchQuery.Filter(
                    Context.RentalPosts.VisibleToTenant().Where(p => recommendedIds.Contains(p.RentalPostId)), input.Criteria())
                    .Select(p => p.RentalPostId).ToListAsync();
                model.Recommendations = recommended.Where(p => eligibleIds.Contains(p.RentalPostId)).Take(3)
                    .Select(p => TenantRentalDiscovery.Card(p, savedIds.Contains(p.RentalPostId))).ToList();
            } catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "Gợi ý phòng chưa sẵn sàng."); }
        }
        model.CurrentUrl = Url.Action(nameof(Index), "RentalPost", input.RouteValues())!;
        foreach (var room in model.Rooms.Concat(model.Recommendations)) room.ReturnUrl = model.CurrentUrl;
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest") return PartialView("_Results", model);
        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int? id, string? returnUrl)
    {
        var model = await TenantRentalDiscovery.DetailsAsync(Context, id, CurrentUserId);
        if (model == null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("Unavailable");
        }
        model.BackUrl = Url.IsLocalUrl(returnUrl) ? returnUrl! : Url.Action(nameof(Index))!;
        if (model.BackUrl == Url.Action("Index", "Dashboard")) model.BackLabel = "Quay lại Trang chủ";
        model.Room.ReturnUrl = Url.Action(nameof(Details), new { id, returnUrl = model.BackUrl })!;
        return View(model);
    }
}
