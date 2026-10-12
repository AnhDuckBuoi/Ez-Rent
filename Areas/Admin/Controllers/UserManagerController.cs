using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace EZ_Rent.Areas_Admin_Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UserManagerController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        [TempData]
        public string? StatusMessage { get; set; }

        private readonly RoleManager<IdentityRole> _roleManager;

        public UserManagerController(
            AppDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: UserManager
        public async Task<IActionResult> Index()
        {
            return View(await _context.Users.ToListAsync());
        }

        // GET: UserManager/Details/5
        public async Task<IActionResult> Details(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var applicationUser = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (applicationUser == null)
            {
                return NotFound();
            }
            var currentRoles = await _userManager.GetRolesAsync(applicationUser);

            ViewBag.CurrentRoles = currentRoles;
            return View(applicationUser);
        }

        // GET: UserManager/Create
        public async Task<IActionResult> Create()
        {
            await LoadRoles();
            return View();
        }

        // POST: UserManager/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
    string fullName,
    string email,
    string password,
    string phoneNumber,
    string[] roles)
        {
            var user = new ApplicationUser()
            {
                FullName = fullName,
                Email = email,
                UserName = email,
                PhoneNumber = phoneNumber
            };

            var result = await _userManager.CreateAsync(user, password);

            if (result.Succeeded)
            {
                if (roles.Length > 0)
                {
                    await _userManager.AddToRolesAsync(user, roles);
                }

                StatusMessage = "Tạo người dùng mới thành công";

                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError("", error.Description);
            }

            StatusMessage = "Tạo người dùng mới thất bại";

            await LoadRoles(roles);

            return View(user);
        }

        // GET: UserManager/Edit/5
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var applicationUser = await _userManager.FindByIdAsync(id);

            if (applicationUser == null)
            {
                return NotFound();
            }

            var currentRoles = await _userManager.GetRolesAsync(applicationUser);

            await LoadRoles(currentRoles);

            return View(applicationUser);
        }

        // POST: UserManager/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(
    string id,
    string fullName,
    string email,
    string? phoneNumber,
    string[] roles)
        {
            var user = await _userManager.FindByIdAsync(id);

            if (user == null)
            {
                return NotFound();
            }

            // Lấy role cũ trước khi thay đổi
            var oldRoles = await _userManager.GetRolesAsync(user);

            // Cập nhật thông tin User
            user.FullName = fullName;
            user.Email = email;
            user.PhoneNumber = phoneNumber;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                }

                await LoadRoles(roles);

                StatusMessage = "Cập nhật thất bại";

                return View(user);
            }

            // Xóa toàn bộ Role cũ
            await _userManager.RemoveFromRolesAsync(user, oldRoles);

            // Thêm Role mới
            if (roles.Length > 0)
            {
                await _userManager.AddToRolesAsync(user, roles);
            }

            StatusMessage = "Cập nhật thành công";

            return RedirectToAction(nameof(Index));
        }
        // GET: UserManager/Delete/5
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null)
            {
                return NotFound();
            }
            var roles = await _roleManager.Roles
                .OrderBy(x => x.Name)
                .ToListAsync();

            ViewBag.Roles = roles;
            var applicationUser = await _context.Users
                .FirstOrDefaultAsync(m => m.Id == id);
            if (applicationUser == null)
            {
                return NotFound();
            }

            return View(applicationUser);
        }

        // POST: UserManager/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var applicationUser = await _context.Users.FindAsync(id);
            if (applicationUser != null)
            {
                _context.Users.Remove(applicationUser);
            }

            await _context.SaveChangesAsync();
            StatusMessage = "Xóa thành công";
            return RedirectToAction(nameof(Index));
        }

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

        private bool ApplicationUserExists(string id)
        {
            return _context.Users.Any(e => e.Id == id);
        }
        private async Task LoadRoles(IEnumerable<string>? selectedRoles = null)
        {
            ViewBag.Roles = await _roleManager.Roles
                .OrderBy(x => x.Name)
                .ToListAsync();

            ViewBag.SelectedRoles = selectedRoles?.ToList()
                                    ?? new List<string>();
        }
    }
}
