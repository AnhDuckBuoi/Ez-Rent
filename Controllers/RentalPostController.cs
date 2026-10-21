using EZ_Rent.Data;
using EZ_Rent.Helpers;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;
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
            if (page < 1) page = 1;

            var query = _context.RentalPosts
                .Include(p => p.Owner)
                .Include(p => p.RoomImages)
                .Where(p => p.ApprovalStatus == ApprovalStatus.Approved &&
                            p.RoomStatus != RoomStatus.Hidden)
                .AsQueryable();

            // FILTER
            if (!string.IsNullOrEmpty(city))
                query = query.Where(p => p.City == city);

            if (!string.IsNullOrEmpty(district))
                query = query.Where(p => p.District == district);

            if (minPrice.HasValue)
                query = query.Where(p => p.Price >= minPrice.Value);

            if (maxPrice.HasValue)
                query = query.Where(p => p.Price <= maxPrice.Value);

            if (roomType.HasValue)
                query = query.Where(p => p.RoomType == roomType.Value);

            if (minArea.HasValue)
                query = query.Where(p => p.Area >= minArea.Value);

            if (maxArea.HasValue)
                query = query.Where(p => p.Area <= maxArea.Value);

            if (amenities != null && amenities.Length > 0)
            {
                foreach (var a in amenities)
                {
                    var amenity = a;
                    query = query.Where(p => p.AmenitiesJson != null &&
                                             p.AmenitiesJson.Contains(amenity));
                }
            }

            // SORT
            query = sortBy switch
            {
                "price_asc" => query.OrderBy(p => p.Price),
                "price_desc" => query.OrderByDescending(p => p.Price),
                "area_asc" => query.OrderBy(p => p.Area),
                "area_desc" => query.OrderByDescending(p => p.Area),
                _ => query.OrderByDescending(p => p.PostedAt ?? p.CreatedAt)
            };

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