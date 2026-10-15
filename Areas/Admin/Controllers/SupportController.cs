using EZ_Rent.Data;
using EZ_Rent.Helpers;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class SupportController : Controller
    {
        private readonly AppDbContext _context;

        private const int PAGE_SIZE = 10;

        public SupportController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // GET: /Admin/Support?status=Pending&page=1
        // ============================================================
        public async Task<IActionResult> Index(string? status, int page = 1)
        {
            if (page < 1) page = 1;

            var query = _context.RepairRequests
                .Include(r => r.RentalPost)
                .Include(r => r.Tenant)
                .AsQueryable();

            // Filter theo status
            if (Enum.TryParse<RepairRequestStatus>(status, out var st))
                query = query.Where(r => r.Status == st);

            query = query.OrderByDescending(r => r.CreatedAt);

            var paginatedRepairs = await PaginatedList<RepairRequest>.CreateAsync(
                query, page, PAGE_SIZE);

            ViewBag.StatusFilter = status;
            return View(paginatedRepairs);
        }

        // ============================================================
        // GET: /Admin/Support/Details/5
        // ============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var repair = await _context.RepairRequests
                .Include(r => r.RentalPost)
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(m => m.RepairRequestId == id);

            if (repair == null) return NotFound();

            return View(repair);
        }

        // ============================================================
        // POST: /Admin/Support/InProgress/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> InProgress(int id)
        {
            var repair = await _context.RepairRequests.FindAsync(id);
            if (repair == null)
                return Json(new { success = false, message = "Không tìm thấy khiếu nại" });

            if (repair.Status != RepairRequestStatus.Pending)
                return Json(new { success = false, message = "Chỉ có thể xử lý khiếu nại đang chờ" });

            repair.Status    = RepairRequestStatus.InProgress;
            repair.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success   = true,
                message   = "Đã chuyển khiếu nại sang trạng thái Đang xử lý",
                newStatus = "InProgress"
            });
        }

        // ============================================================
        // POST: /Admin/Support/Resolve/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(int id, string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return Json(new { success = false, message = "Vui lòng nhập ghi chú giải quyết" });

            var repair = await _context.RepairRequests.FindAsync(id);
            if (repair == null)
                return Json(new { success = false, message = "Không tìm thấy khiếu nại" });

            if (repair.Status == RepairRequestStatus.Resolved)
                return Json(new { success = false, message = "Khiếu nại đã được giải quyết rồi" });

            repair.Status         = RepairRequestStatus.Resolved;
            repair.ResolutionNote = note;
            repair.ResolvedAt     = DateTime.UtcNow;
            repair.UpdatedAt      = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success   = true,
                message   = "Đã đánh dấu khiếu nại đã giải quyết",
                newStatus = "Resolved"
            });
        }

        // ============================================================
        // POST: /Admin/Support/Cancel/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id, string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return Json(new { success = false, message = "Vui lòng nhập lý do hủy" });

            var repair = await _context.RepairRequests.FindAsync(id);
            if (repair == null)
                return Json(new { success = false, message = "Không tìm thấy khiếu nại" });

            if (repair.Status == RepairRequestStatus.Resolved)
                return Json(new { success = false, message = "Không thể hủy khiếu nại đã giải quyết" });

            repair.Status         = RepairRequestStatus.Cancelled;
            repair.ResolutionNote = note;
            repair.UpdatedAt      = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Json(new
            {
                success   = true,
                message   = "Đã hủy khiếu nại",
                newStatus = "Cancelled"
            });
        }
    }
}