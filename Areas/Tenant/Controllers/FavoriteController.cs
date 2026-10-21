using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EZ_Rent.Areas.Tenant.Controllers
{
    [Area("Tenant")]
    [Authorize(Roles = "Tenant")]
    public class FavoriteController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RecommendationService _recommender;

        public FavoriteController(
            AppDbContext context,
            RecommendationService recommender)
        {
            _context = context;
            _recommender = recommender;
        }

        // ============================================================
        // GET: /Tenant/Favorite
        // ============================================================
        public async Task<IActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return Unauthorized();

            var favorites = await _context.Favorites
                .Include(f => f.RentalPost)
                    .ThenInclude(p => p.RoomImages)
                .Include(f => f.RentalPost)
                    .ThenInclude(p => p.Owner)
                .Where(f => f.UserId == userId)
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            return View(favorites);
        }

        // ============================================================
        // POST: /Tenant/Favorite/Toggle/{rentalPostId}
        // AJAX — thêm / bỏ yêu thích
        // ============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int rentalPostId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Json(new { success = false, message = "Chưa đăng nhập" });

            // Check post tồn tại
            var post = await _context.RentalPosts.FindAsync(rentalPostId);
            if (post == null)
                return Json(new { success = false, message = "Phòng không tồn tại" });

            // Tìm Favorite hiện tại
            var existing = await _context.Favorites
                .FirstOrDefaultAsync(f => f.UserId == userId &&
                                          f.RentalPostId == rentalPostId);

            if (existing != null)
            {
                // ===== BỎ YÊU THÍCH =====
                _context.Favorites.Remove(existing);
                await _context.SaveChangesAsync();

                _recommender.InvalidateModel();

                return Json(new
                {
                    success = true,
                    action  = "removed",
                    message = "Đã bỏ yêu thích"
                });
            }

            // ===== THÊM YÊU THÍCH =====
            _context.Favorites.Add(new Favorite
            {
                UserId       = userId,
                RentalPostId = rentalPostId,
                CreatedAt    = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            _recommender.InvalidateModel();

            return Json(new
            {
                success = true,
                action  = "added",
                message = "Đã thêm vào yêu thích"
            });
        }
    }
}