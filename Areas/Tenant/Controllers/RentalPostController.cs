using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
public class RentalPostController : TenantControllerBase
{
    public RentalPostController(AppDbContext context) : base(context) { }

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
        model.CurrentUrl = Url.Action(nameof(Index), "RentalPost", input.RouteValues())!;
        foreach (var room in model.Rooms) room.ReturnUrl = model.CurrentUrl;
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
