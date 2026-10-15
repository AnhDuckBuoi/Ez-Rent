using EZ_Rent.Helpers;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class RoleManagerController : Controller
    {
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly UserManager<ApplicationUser> _userManager;

        public RoleManagerController(
            RoleManager<IdentityRole> roleManager,
            UserManager<ApplicationUser> userManager)
        {
            _roleManager = roleManager;
            _userManager = userManager;
        }

        // ============================================================
        // GET: /Admin/RoleManager
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var roles = await _roleManager.Roles
                .OrderBy(r => r.Name)
                .ToListAsync();

            // Đếm số user mỗi role
            var roleUserCounts = new Dictionary<string, int>();
            foreach (var role in roles)
            {
                if (!string.IsNullOrEmpty(role.Name))
                {
                    var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
                    roleUserCounts[role.Name] = usersInRole.Count;
                }
            }

            ViewBag.RoleUserCounts = roleUserCounts;

            return View(roles);
        }

        // ============================================================
        // GET: /Admin/RoleManager/Create
        // ============================================================
        public IActionResult Create()
        {
            return View();
        }

        // ============================================================
        // POST: /Admin/RoleManager/Create
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string roleName)
        {
            if (string.IsNullOrWhiteSpace(roleName))
            {
                ModelState.AddModelError("", "Tên Role không được để trống");
                return View();
            }

            roleName = roleName.Trim();

            if (await _roleManager.RoleExistsAsync(roleName))
            {
                ModelState.AddModelError("", "Role đã tồn tại");
                return View();
            }

            var result = await _roleManager.CreateAsync(new IdentityRole(roleName));

            if (result.Succeeded)
            {
                TempData["StatusMessage"] = $"Tạo Role \"{roleName}\" thành công!";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View();
        }

        // ============================================================
        // POST: /Admin/RoleManager/Delete/{id}
        // AJAX — trả về JSON để JS cập nhật DOM
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var role = await _roleManager.FindByIdAsync(id);
            if (role == null)
                return Json(ApiResult.Fail("Role không tồn tại"));

            if (string.IsNullOrEmpty(role.Name))
                return Json(ApiResult.Fail("Role không hợp lệ"));

            // ⚠️ KHÔNG cho xóa các role hệ thống
            var protectedRoles = new[] { "Admin", "Landlord", "Tenant" };
            if (protectedRoles.Contains(role.Name, StringComparer.OrdinalIgnoreCase))
                return Json(ApiResult.Fail($"Không thể xóa role hệ thống \"{role.Name}\""));

            // ⚠️ KHÔNG cho xóa nếu còn user giữ role
            var usersInRole = await _userManager.GetUsersInRoleAsync(role.Name);
            if (usersInRole.Any())
                return Json(ApiResult.Fail(
                    $"Không thể xóa: còn {usersInRole.Count} user đang giữ role \"{role.Name}\""));

            var result = await _roleManager.DeleteAsync(role);

            if (result.Succeeded)
                return Json(ApiResult.Ok($"Đã xóa role \"{role.Name}\""));

            var errors = string.Join(", ", result.Errors.Select(e => e.Description));
            return Json(ApiResult.Fail($"Lỗi: {errors}"));
        }
    }
}