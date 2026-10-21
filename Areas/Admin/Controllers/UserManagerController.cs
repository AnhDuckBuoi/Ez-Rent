using EZ_Rent.Data;
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
    public class UserManagerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        private const int PAGE_SIZE = 15;

        [TempData]
        public string? StatusMessage { get; set; }

        public UserManagerController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // ============================================================
        // GET: /Admin/UserManager
        // ============================================================
        public async Task<IActionResult> Index(int page = 1)
        {
            if (page < 1) page = 1;

            var query = _context.Users
                .OrderByDescending(u => u.CreatedAt)
                .AsQueryable();

            var paginatedUsers = await PaginatedList<ApplicationUser>.CreateAsync(
                query, page, PAGE_SIZE);

            // ⭐ PRE-LOAD ROLES 1 LẦN — tránh N+1 query
            var userRoles = new Dictionary<string, List<string>>();
            foreach (var user in paginatedUsers)
            {
                var roles = await _userManager.GetRolesAsync(user);
                userRoles[user.Id] = roles.ToList();
            }

            ViewBag.UserRoles = userRoles;

            return View(paginatedUsers);
        }

        // ============================================================
        // GET: /Admin/UserManager/Details/{id}
        // ============================================================
        public async Task<IActionResult> Details(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _context.Users.FirstOrDefaultAsync(m => m.Id == id);
            if (user == null) return NotFound();

            ViewBag.CurrentRoles = await _userManager.GetRolesAsync(user);
            return View(user);
        }

        // ============================================================
        // GET: /Admin/UserManager/Create
        // ============================================================
        public async Task<IActionResult> Create()
        {
            await LoadRoles();
            return View();
        }

        // ============================================================
        // POST: /Admin/UserManager/Create
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            string fullName,
            string email,
            string password,
            string? phoneNumber,
            string[]? roles)
        {
            var user = new ApplicationUser
            {
                FullName = fullName,
                Email = email,
                UserName = email,
                PhoneNumber = phoneNumber
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                if (roles != null && roles.Length > 0)
                    await _userManager.AddToRolesAsync(user, roles);

                StatusMessage = "Tạo người dùng mới thành công";
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            await LoadRoles(roles);
            return View(user);
        }

        // ============================================================
        // GET: /Admin/UserManager/Edit/{id}
        // ============================================================
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var currentRoles = await _userManager.GetRolesAsync(user);
            await LoadRoles(currentRoles);

            return View(user);
        }

        // ============================================================
        // POST: /Admin/UserManager/Edit/{id}
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
            string id,
            string fullName,
            string email,
            string? phoneNumber,
            string[]? roles)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var oldRoles = await _userManager.GetRolesAsync(user);

            user.FullName = fullName;
            user.Email = email;
            user.PhoneNumber = phoneNumber;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);

                await LoadRoles(roles);
                return View(user);
            }

            await _userManager.RemoveFromRolesAsync(user, oldRoles);

            if (roles != null && roles.Length > 0)
                await _userManager.AddToRolesAsync(user, roles);

            StatusMessage = "Cập nhật thành công";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // GET: /Admin/UserManager/Delete/{id}
        // ============================================================
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _context.Users.FirstOrDefaultAsync(m => m.Id == id);
            if (user == null) return NotFound();

            return View(user);
        }

        // ============================================================
        // POST: /Admin/UserManager/Delete/{id}
        // ============================================================
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (user.Id == _userManager.GetUserId(User))
            {
                StatusMessage = "Không thể xóa chính mình!";
                return RedirectToAction(nameof(Index));
            }

            await _userManager.DeleteAsync(user);

            StatusMessage = "Xóa thành công";
            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // POST: /Admin/UserManager/Lock/{id}
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Lock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy user" });

            if (user.Id == _userManager.GetUserId(User))
                return Json(new { success = false, message = "Không thể khóa chính mình!" });

            if (await _userManager.IsInRoleAsync(user, "Admin"))
                return Json(new { success = false, message = "Không thể khóa Admin khác!" });

            user.Status = EZ_Rent.Models.Enums.UserStatus.Locked;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            return Json(new
            {
                success = true,
                message = $"Đã khóa tài khoản {user.FullName}",
                newStatus = "Locked"
            });
        }

        // ============================================================
        // POST: /Admin/UserManager/Unlock/{id}
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Unlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return Json(new { success = false, message = "Không tìm thấy user" });

            user.Status = EZ_Rent.Models.Enums.UserStatus.Active;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, null);
            await _userManager.ResetAccessFailedCountAsync(user);

            return Json(new
            {
                success = true,
                message = $"Đã mở khóa tài khoản {user.FullName}",
                newStatus = "Active"
            });
        }

        // ============================================================
        // Helpers
        // ============================================================
        private async Task LoadRoles(IEnumerable<string>? selectedRoles = null)
        {
            ViewBag.Roles = await _roleManager.Roles
                .OrderBy(x => x.Name)
                .ToListAsync();

            ViewBag.SelectedRoles = selectedRoles?.ToList() ?? new List<string>();
        }
    }
}