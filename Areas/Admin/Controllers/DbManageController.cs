using EZ_Rent.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;

namespace EZ_Rent.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DbManageController : Controller
    {
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DbManageController> _logger;

        [TempData]
        public string? StatusMessage { get; set; }

        [TempData]
        public string? StatusType { get; set; }   // "success" | "danger" | "warning"

        public DbManageController(
            AppDbContext dbContext,
            IWebHostEnvironment env,
            ILogger<DbManageController> logger)
        {
            _dbContext = dbContext;
            _env = env;
            _logger = logger;
        }

        // ============================================================
        // GET: /Admin/DbManage
        // ============================================================
        public async Task<IActionResult> Index()
        {
            // ⚠️ Chỉ cho phép ở Development
            if (!_env.IsDevelopment())
            {
                TempData["StatusMessage"] = "Chức năng này chỉ khả dụng ở môi trường Development.";
                TempData["StatusType"] = "warning";
                return RedirectToAction("Index", "Dashboard");
            }

            var canConnect = await _dbContext.Database.CanConnectAsync();

            ViewBag.CanConnect  = canConnect;
            ViewBag.DbName      = _dbContext.Database.GetDbConnection().Database;
            ViewBag.DataSource  = _dbContext.Database.GetDbConnection().DataSource;
            ViewBag.Provider    = _dbContext.Database.ProviderName;

            if (canConnect)
            {
                ViewBag.AppliedMigrations = (await _dbContext.Database.GetAppliedMigrationsAsync()).ToList();
                ViewBag.PendingMigrations = (await _dbContext.Database.GetPendingMigrationsAsync()).ToList();

                // Đếm record mỗi bảng chính
                try
                {
                    ViewBag.TableCounts = new Dictionary<string, int>
                    {
                        ["Users"]            = await _dbContext.Users.CountAsync(),
                        ["RentalPosts"]      = await _dbContext.RentalPosts.CountAsync(),
                        ["RentalContracts"]  = await _dbContext.RentalContracts.CountAsync(),
                        ["Invoices"]         = await _dbContext.Invoices.CountAsync(),
                        ["RepairRequests"]   = await _dbContext.RepairRequests.CountAsync(),
                        ["Notifications"]    = await _dbContext.Notifications.CountAsync(),
                    };
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Không đếm được số record");
                    ViewBag.TableCounts = new Dictionary<string, int>();
                }
            }

            return View();
        }

        // ============================================================
        // GET: /Admin/DbManage/DeleteDb
        // ============================================================
        [HttpGet]
        public IActionResult DeleteDb()
        {
            if (!_env.IsDevelopment())
            {
                TempData["StatusMessage"] = "Chức năng này chỉ khả dụng ở môi trường Development.";
                TempData["StatusType"] = "warning";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.DbName = _dbContext.Database.GetDbConnection().Database;
            return View();
        }

        // ============================================================
        // POST: /Admin/DbManage/DeleteDb
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteDbAsync(string confirmName)
        {
            if (!_env.IsDevelopment())
                return Forbid();

            var dbName = _dbContext.Database.GetDbConnection().Database;

            // ⚠️ BẮT BUỘC nhập đúng tên DB để xác nhận
            if (string.IsNullOrWhiteSpace(confirmName) ||
                !string.Equals(confirmName.Trim(), dbName, StringComparison.OrdinalIgnoreCase))
            {
                TempData["StatusMessage"] = $"Tên xác nhận không khớp. Bạn phải nhập chính xác: \"{dbName}\"";
                TempData["StatusType"] = "danger";
                return RedirectToAction(nameof(DeleteDb));
            }

            try
            {
                var success = await _dbContext.Database.EnsureDeletedAsync();

                if (success)
                {
                    _logger.LogWarning("⚠️ ADMIN {User} đã XÓA DATABASE {DbName}",
                        User.Identity?.Name, dbName);

                    TempData["StatusMessage"] = $"Đã xóa database \"{dbName}\" thành công.";
                    TempData["StatusType"] = "success";
                }
                else
                {
                    TempData["StatusMessage"] = "Database không tồn tại hoặc không thể xóa.";
                    TempData["StatusType"] = "warning";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi xóa database");
                TempData["StatusMessage"] = "Lỗi khi xóa database: " + ex.Message;
                TempData["StatusType"] = "danger";
            }

            return RedirectToAction(nameof(Index));
        }

        // ============================================================
        // POST: /Admin/DbManage/Migrate
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Migrate()
        {
            if (!_env.IsDevelopment())
                return Forbid();

            try
            {
                await _dbContext.Database.MigrateAsync();

                _logger.LogInformation("✅ ADMIN {User} đã chạy migration",
                    User.Identity?.Name);

                TempData["StatusMessage"] = "Đã cập nhật database thành công.";
                TempData["StatusType"] = "success";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi migration");
                TempData["StatusMessage"] = "Lỗi migration: " + ex.Message;
                TempData["StatusType"] = "danger";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}