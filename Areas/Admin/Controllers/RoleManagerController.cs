using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class RoleManagerController : Controller
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public RoleManagerController(RoleManager<IdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public IActionResult Index()
    {
        return View(_roleManager.Roles.ToList());
    }

    public IActionResult Create()
    {
        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(string roleName)
    {
        if (string.IsNullOrWhiteSpace(roleName))
        {
            ModelState.AddModelError("", "Tên Role không được để trống");
            return View();
        }

        if (await _roleManager.RoleExistsAsync(roleName))
        {
            ModelState.AddModelError("", "Role đã tồn tại");
            return View();
        }

        var result = await _roleManager.CreateAsync(
            new IdentityRole(roleName));

        if (result.Succeeded)
        {
            TempData["StatusMessage"] = "Tạo Role thành công!";
            return RedirectToAction(nameof(Index));
        }

        foreach (var error in result.Errors)
        {
            ModelState.AddModelError("", error.Description);
        }

        return View();
    }
}