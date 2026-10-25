using EZ_Rent.Data;
using EZ_Rent.Helpers;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;
using EZ_Rent.Services.Discovery;
using EZ_Rent.Areas.Tenant.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EZ_Rent.Controllers
{
    public class RentalPostController : Controller
    {
        private readonly AppDbContext _context;
        private readonly RecommendationService _recommender;

        private const int PAGE_SIZE = 9;   // 3x3 grid

        public RentalPostController(
            AppDbContext context,
            RecommendationService recommender)
        {
            _context = context;
            _recommender = recommender;
        }

        public async Task<IActionResult> Index(
    string? city,
    string? district,
    decimal? minPrice,
    decimal? maxPrice,
    RoomType? roomType,
    double? minArea,
    double? maxArea,
    string[]? amenities,
    string? sortBy,
    int page = 1)
        {
            if (User.IsInRole("Tenant"))
            {
                var input = new RentalSearchInput
                {
                    Keyword = Request.Query["Keyword"].FirstOrDefault(), City = city, District = district,
                    MinPrice = minPrice, MaxPrice = maxPrice, MinArea = minArea, MaxArea = maxArea,
                    RoomType = roomType, Amenities = string.Join(",", amenities ?? []),
                    Sort = (sortBy ?? "newest").Replace('_', '-'), Page = page
                };
                input.Normalize();
                var values = input.RouteValues(); values["area"] = "Tenant";
                return RedirectToAction("Index", "RentalPost", values);
            }
            if (page < 1) page = 1;

            var query = RentalSearchQuery.Filter(_context.RentalPosts.AsNoTracking().PublicListings()
                .Include(p => p.Owner).Include(p => p.RoomImages), new RentalSearchCriteria
            {
                Keyword = Request.Query["Keyword"].FirstOrDefault(), City = city, District = district,
                MinPrice = minPrice, MaxPrice = maxPrice, MinArea = minArea, MaxArea = maxArea,
                RoomType = roomType, Amenities = amenities ?? []
            });
            query = RentalSearchQuery.Order(query, sortBy, _context.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
                .ThenByDescending(p => p.RentalPostId);

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)PAGE_SIZE);
            if (page > totalPages && totalPages > 0)
                page = totalPages;

            var paginatedPosts = await PaginatedList<RentalPost>.CreateAsync(
                query, page, PAGE_SIZE);

            // ⭐ LƯU SEARCH HISTORY — dùng method riêng
            await SaveSearchHistoryAsync(city, district, minPrice, maxPrice, roomType);

            // ViewBag
            ViewBag.Cities = await _context.RentalPosts
                .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
                .Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync();

            ViewBag.Districts = await _context.RentalPosts
                .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
                .Select(p => p.District).Distinct().OrderBy(d => d).ToListAsync();

            ViewBag.City = city;
            ViewBag.District = district;
            ViewBag.MinPrice = minPrice;
            ViewBag.MaxPrice = maxPrice;
            ViewBag.RoomType = roomType;
            ViewBag.MinArea = minArea;
            ViewBag.MaxArea = maxArea;
            ViewBag.Amenities = amenities ?? Array.Empty<string>();
            ViewBag.SortBy = sortBy;

            return View(paginatedPosts);
        }

        // ============================================================
        // ⭐ SAVE SEARCH HISTORY — dùng chung cho Home + RentalPost
        // ============================================================
        private async Task SaveSearchHistoryAsync(
            string? city,
            string? district,
            decimal? minPrice,
            decimal? maxPrice,
            RoomType? roomType)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return;

            // Nới điều kiện: chỉ cần 1 tiêu chí là lưu
            bool hasFilter =
                !string.IsNullOrWhiteSpace(city) ||
                !string.IsNullOrWhiteSpace(district) ||
                minPrice.HasValue ||
                maxPrice.HasValue ||
                roomType.HasValue;

            if (!hasFilter) return;

            try
            {
                await SearchHistoryRecorder.RecordAsync(_context, _recommender, userId,
                    city, district, minPrice, maxPrice, roomType, HttpContext.RequestAborted);
            }
            catch (Exception ex)
            {
                // Không nên crash trang search nếu save history fail
                Console.WriteLine($"[SearchHistory] Lỗi lưu: {ex.Message}");
            }
        }

        // GET: /RentalPost/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (User.IsInRole("Tenant"))
                return RedirectToAction("Details", "RentalPost", new { area = "Tenant", id });
            if (id == null) return NotFound();

            var post = await _context.RentalPosts
                .Include(p => p.Owner)
                .Include(p => p.RoomImages)
                .Include(p => p.Services)
                .FirstOrDefaultAsync(m => m.RentalPostId == id &&
                                          m.ApprovalStatus == ApprovalStatus.Approved);

            if (post == null) return NotFound();

            return View(post);
        }
    }
}