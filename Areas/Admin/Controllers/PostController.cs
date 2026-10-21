using EZ_Rent.Data;
using EZ_Rent.Helpers;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EZ_Rent.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PostController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RecommendationService _recommender;

        private const int PAGE_SIZE = 10;

        public PostController(AppDbContext context, RecommendationService recommender)
        {
            _context = context;
            _recommender = recommender;
        }

        // ============================================================
        // GET: /Admin/Post?status=Pending&page=1
        // ============================================================
        public async Task<IActionResult> Index(string? status, int page = 1)
        {
            if (page < 1) page = 1;

            var query = _context.RentalPosts
                .Include(p => p.Owner)
                .AsQueryable();

            // ===== FILTER theo ApprovalStatus =====
            if (Enum.TryParse<ApprovalStatus>(status, out var st))
                query = query.Where(p => p.ApprovalStatus == st);

            query = query.OrderByDescending(p => p.CreatedAt);

            var paginatedPosts = await PaginatedList<RentalPost>.CreateAsync(
                query, page, PAGE_SIZE);

            // ===== Đếm số lượng theo từng status (cho tabs) =====
            ViewBag.CountAll = await _context.RentalPosts.CountAsync();
            ViewBag.CountPending = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Pending);
            ViewBag.CountApproved = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Approved);
            ViewBag.CountRejected = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Rejected);
            ViewBag.CountNeedRevision = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.NeedRevision);
            ViewBag.CountDraft = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Draft);

            ViewBag.StatusFilter = status;

            return View(paginatedPosts);
        }

        // ============================================================
        // GET: /Admin/Post/Details/5
        // ============================================================
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var post = await _context.RentalPosts
                .Include(p => p.Owner)
                .Include(p => p.ApprovedBy)
                .Include(p => p.RoomImages)
                .Include(p => p.Services)
                .FirstOrDefaultAsync(m => m.RentalPostId == id);

            if (post == null) return NotFound();

            return View(post);
        }

        // ============================================================
        // POST: /Admin/Post/Approve/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int id)
        {
            var post = await _context.RentalPosts.FindAsync(id);
            if (post == null)
                return Json(ApiResult.Fail("Bài viết không tồn tại"));

            if (post.ApprovalStatus == ApprovalStatus.Approved)
                return Json(ApiResult.Fail("Bài viết đã được duyệt rồi"));

            post.ApprovalStatus = ApprovalStatus.Approved;
            post.RoomStatus = RoomStatus.Available;
            post.ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
            post.ApprovedAt = DateTime.UtcNow;
            post.PostedAt = DateTime.UtcNow;
            post.ApprovalNote = null;

            await _context.SaveChangesAsync();
            _recommender.InvalidateModel();

            return Json(ApiResult.Ok($"Đã duyệt bài viết \"{post.Title}\""));
        }

        // ============================================================
        // POST: /Admin/Post/Reject/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id, string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return Json(ApiResult.Fail("Vui lòng nhập lý do từ chối"));

            var post = await _context.RentalPosts.FindAsync(id);
            if (post == null)
                return Json(ApiResult.Fail("Bài viết không tồn tại"));

            post.ApprovalStatus = ApprovalStatus.Rejected;
            post.RoomStatus = RoomStatus.Hidden;
            post.ApprovalNote = note;
            post.ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
            post.ApprovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _recommender.InvalidateModel();

            return Json(ApiResult.Ok($"Đã từ chối bài viết \"{post.Title}\""));
        }

        // ============================================================
        // POST: /Admin/Post/NeedRevision/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NeedRevision(int id, string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return Json(ApiResult.Fail("Vui lòng nhập nội dung cần sửa"));

            var post = await _context.RentalPosts.FindAsync(id);
            if (post == null)
                return Json(ApiResult.Fail("Bài viết không tồn tại"));

            post.ApprovalStatus = ApprovalStatus.NeedRevision;
            post.RoomStatus = RoomStatus.Hidden;
            post.ApprovalNote = note;
            post.ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
            post.ApprovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _recommender.InvalidateModel();

            return Json(ApiResult.Ok($"Đã yêu cầu sửa bài viết \"{post.Title}\""));
        }

        // ============================================================
        // POST: /Admin/Post/Revoke/5
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revoke(int id)
        {
            var post = await _context.RentalPosts.FindAsync(id);
            if (post == null)
                return Json(ApiResult.Fail("Bài viết không tồn tại"));

            if (post.ApprovalStatus != ApprovalStatus.Approved)
                return Json(ApiResult.Fail("Chỉ có thể hủy duyệt bài viết đã được duyệt"));

            post.ApprovalStatus = ApprovalStatus.Pending;
            post.RoomStatus = RoomStatus.Hidden;
            post.ApprovedById = null;
            post.ApprovedAt = null;
            post.PostedAt = null;
            post.ApprovalNote = null;

            await _context.SaveChangesAsync();
            _recommender.InvalidateModel();

            return Json(ApiResult.Ok($"Đã hủy duyệt bài viết \"{post.Title}\""));
        }
        public async Task<IActionResult> Index(string? status, string? search, int page = 1)
        {
            if (page < 1) page = 1;

            var query = _context.RentalPosts
                .Include(p => p.Owner)
                .AsQueryable();

            // Filter status
            if (Enum.TryParse<ApprovalStatus>(status, out var st))
                query = query.Where(p => p.ApprovalStatus == st);

            // ⭐ SEARCH
            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim().ToLower();
                query = query.Where(p =>
                    p.Title.ToLower().Contains(s) ||
                    p.Address.ToLower().Contains(s) ||
                    p.City.ToLower().Contains(s) ||
                    (p.District != null && p.District.ToLower().Contains(s)) ||
                    (p.Owner != null && p.Owner.FullName.ToLower().Contains(s)));
            }

            query = query.OrderByDescending(p => p.CreatedAt);

            var paginatedPosts = await PaginatedList<RentalPost>.CreateAsync(
                query, page, PAGE_SIZE);

            // Counts cho tabs
            ViewBag.CountAll = await _context.RentalPosts.CountAsync();
            ViewBag.CountPending = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Pending);
            ViewBag.CountApproved = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Approved);
            ViewBag.CountRejected = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Rejected);
            ViewBag.CountNeedRevision = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.NeedRevision);
            ViewBag.CountDraft = await _context.RentalPosts.CountAsync(p => p.ApprovalStatus == ApprovalStatus.Draft);

            ViewBag.StatusFilter = status;
            ViewBag.Search = search;

            return View(paginatedPosts);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(int id, string? note)
        {
            if (string.IsNullOrWhiteSpace(note))
                return Json(ApiResult.Fail("Vui lòng nhập lý do gỡ bài"));

            var post = await _context.RentalPosts.FindAsync(id);
            if (post == null)
                return Json(ApiResult.Fail("Bài viết không tồn tại"));

            // Không cho gỡ nếu đang có hợp đồng Active
            var hasActiveContract = await _context.RentalContracts
                .AnyAsync(c => c.RentalPostId == id &&
                               c.Status == ContractStatus.Active);

            if (hasActiveContract)
                return Json(ApiResult.Fail(
                    "Không thể gỡ: phòng đang có hợp đồng Active"));

            post.ApprovalStatus = ApprovalStatus.Removed;
            post.RoomStatus = RoomStatus.Hidden;
            post.ApprovalNote = $"[GỠ] {note}";
            post.ApprovedById = User.FindFirstValue(ClaimTypes.NameIdentifier);
            post.ApprovedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            _recommender.InvalidateModel();

            return Json(ApiResult.Ok($"Đã gỡ bài viết \"{post.Title}\""));
        }
    }

}