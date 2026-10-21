using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
public class FavoriteController : TenantControllerBase
{
    private readonly EZ_Rent.Services.RecommendationService recommender;
    public FavoriteController(AppDbContext context, EZ_Rent.Services.RecommendationService recommender) : base(context) { this.recommender = recommender; }

    private IQueryable<Favorite> MyFavorites => Context.Favorites.Where(f => f.UserId == CurrentUserId);

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        var model = await TenantRentalDiscovery.FavoritesAsync(Context, CurrentUserId, page);
        model.CurrentUrl = Url.Action(nameof(Index), new { page = model.Filters.Page })!;
        foreach (var room in model.Rooms) room.ReturnUrl = model.CurrentUrl;
        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            return PartialView("~/Areas/Tenant/Views/RentalPost/_Results.cshtml", model);
        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Set(SetFavoriteInput input)
    {
        var ajax = Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        var back = Url.IsLocalUrl(input.ReturnUrl) ? input.ReturnUrl! : Url.Action("Index", "RentalPost")!;
        if (!ModelState.IsValid)
        {
            if (ajax) return BadRequest(new { message = "Yêu cầu lưu phòng không hợp lệ." });
            TempData["DiscoveryError"] = "Yêu cầu lưu phòng không hợp lệ.";
            return LocalRedirect(back);
        }
        if (input.Saved == true)
        {
            if (!await PublicAvailablePosts.AnyAsync(p => p.RentalPostId == input.RentalPostId))
            {
                if (ajax) return NotFound(new { message = "Phòng không còn khả dụng. Vui lòng tải lại danh sách." });
                TempData["DiscoveryError"] = "Phòng không còn khả dụng để lưu.";
                return LocalRedirect(back);
            }
            if (!await MyFavorites.AnyAsync(f => f.RentalPostId == input.RentalPostId))
            {
                var favorite = new Favorite { UserId = CurrentUserId, RentalPostId = input.RentalPostId };
                Context.Favorites.Add(favorite);
                try { await Context.SaveChangesAsync(); recommender.InvalidateModel(); }
                catch (DbUpdateException)
                {
                    Context.Entry(favorite).State = EntityState.Detached;
                    if (!await MyFavorites.AnyAsync(f => f.RentalPostId == input.RentalPostId)) throw;
                }
            }
        }
        else
        {
            // Removing an unavailable post is still allowed, and only affects this Tenant.
            await MyFavorites.Where(f => f.RentalPostId == input.RentalPostId).ExecuteDeleteAsync();
        }
        recommender.InvalidateModel();
        var message = input.Saved == true ? "Đã lưu phòng vào yêu thích." : "Đã bỏ lưu phòng.";
        if (ajax) return Json(new { postId = input.RentalPostId, saved = input.Saved == true, message });
        TempData["DiscoveryMessage"] = message;
        return LocalRedirect(back);
    }

    // Compatibility with the public pages from main. Keep all writes behind Set's ownership/visibility rules.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int rentalPostId)
    {
        if (rentalPostId < 1) return BadRequest();
        var saved = !await MyFavorites.AnyAsync(f => f.RentalPostId == rentalPostId);
        var result = await Set(new SetFavoriteInput { RentalPostId = rentalPostId, Saved = saved });
        return result is JsonResult ? Json(new { success = true, action = saved ? "added" : "removed", message = saved ? "Đã lưu phòng." : "Đã bỏ lưu phòng." }) : result;
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int? id)
    {
        var favorite = await MyFavorites.AsNoTracking()
            .FirstOrDefaultAsync(f => f.FavoriteId == id);
        if (favorite == null) return NotFound();
        var model = await TenantRentalDiscovery.DetailsAsync(Context, favorite.RentalPostId, CurrentUserId);
        if (model == null) return NotFound();
        model.BackUrl = Url.Action(nameof(Index))!;
        model.Room.ReturnUrl = Url.Action("Details", "RentalPost", new { id = favorite.RentalPostId, returnUrl = model.BackUrl })!;
        return View("~/Areas/Tenant/Views/RentalPost/Details.cshtml", model);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await PopulatePostsAsync();
        return View(new FavoriteInputModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(FavoriteInputModel input)
    {
        await ValidatePostAsync(input.RentalPostId);
        if (!ModelState.IsValid)
        {
            await PopulatePostsAsync(input.RentalPostId);
            return View(input);
        }
        if (await MyFavorites.AnyAsync(f => f.RentalPostId == input.RentalPostId))
            return RedirectToAction(nameof(Index));

        var favorite = new Favorite { UserId = CurrentUserId, RentalPostId = input.RentalPostId };
        Context.Favorites.Add(favorite);
        try { await Context.SaveChangesAsync(); recommender.InvalidateModel(); }
        catch (DbUpdateException)
        {
            // A concurrent request may have added the same favorite (unique index).
            Context.Entry(favorite).State = EntityState.Detached;
            if (!await MyFavorites.AnyAsync(f => f.RentalPostId == input.RentalPostId)) throw;
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] int? id)
    {
        var favorite = await MyFavorites.AsNoTracking().FirstOrDefaultAsync(f => f.FavoriteId == id);
        if (favorite == null) return NotFound();
        if (!await PublicAvailablePosts.AnyAsync(p => p.RentalPostId == favorite.RentalPostId)) return NotFound();
        await PopulatePostsAsync(favorite.RentalPostId);
        return View(new FavoriteInputModel { RentalPostId = favorite.RentalPostId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, FavoriteInputModel input)
    {
        var favorite = await MyFavorites.FirstOrDefaultAsync(f => f.FavoriteId == id);
        if (favorite == null) return NotFound();
        await ValidatePostAsync(input.RentalPostId);
        if (await MyFavorites.AnyAsync(f => f.FavoriteId != id && f.RentalPostId == input.RentalPostId))
            ModelState.AddModelError(nameof(input.RentalPostId), "Phòng này đã có trong danh sách yêu thích.");
        if (ModelState.IsValid)
        {
            favorite.RentalPostId = input.RentalPostId;
            try
            {
                await Context.SaveChangesAsync(); recommender.InvalidateModel();
                return RedirectToAction(nameof(Index));
            }
            catch (DbUpdateConcurrencyException) { return NotFound(); }
            catch (DbUpdateException)
            {
                Context.Entry(favorite).State = EntityState.Detached;
                if (!await MyFavorites.AnyAsync(f => f.FavoriteId != id && f.RentalPostId == input.RentalPostId)) throw;
                ModelState.AddModelError(nameof(input.RentalPostId), "Phòng này đã có trong danh sách yêu thích.");
            }
        }
        await PopulatePostsAsync(input.RentalPostId);
        return View(input);
    }

    [HttpGet]
    public async Task<IActionResult> Delete([FromRoute] int? id)
    {
        var favorite = await MyFavorites.AsNoTracking().Include(f => f.RentalPost).Include(f => f.User)
            .FirstOrDefaultAsync(f => f.FavoriteId == id);
        if (favorite == null) return NotFound();
        if (!await PublicAvailablePosts.AnyAsync(p => p.RentalPostId == favorite.RentalPostId))
            favorite.RentalPost = new RentalPost { Address = "Tin không còn khả dụng" };
        return View(favorite);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id)
    {
        var favorite = await MyFavorites.FirstOrDefaultAsync(f => f.FavoriteId == id);
        if (favorite == null) return NotFound();
        Context.Favorites.Remove(favorite);
        try { await Context.SaveChangesAsync(); recommender.InvalidateModel(); }
        catch (DbUpdateConcurrencyException) { return NotFound(); }
        return RedirectToAction(nameof(Index));
    }

    private async Task ValidatePostAsync(int postId)
    {
        if (!await PublicAvailablePosts.AnyAsync(p => p.RentalPostId == postId))
            ModelState.AddModelError(nameof(FavoriteInputModel.RentalPostId), "Phòng không còn được công khai hoặc không còn trống.");
    }

    private async Task PopulatePostsAsync(int? selectedId = null) => ViewData["RentalPostId"] =
        new SelectList(await PublicAvailablePosts.AsNoTracking().OrderBy(p => p.Title)
            .Select(p => new { p.RentalPostId, p.Title }).ToListAsync(), "RentalPostId", "Title", selectedId);
}
