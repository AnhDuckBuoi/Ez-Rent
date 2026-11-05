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

        private const int PAGE_SIZE = 12;   // 4x3 balanced grid

        public RentalPostController(
            AppDbContext context,
            RecommendationService recommender)
        {
            _context = context;
            _recommender = recommender;
        }

        public async Task<IActionResult> Index(
            string? keyword,
            string? search,
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
            var effectiveKeyword = !string.IsNullOrWhiteSpace(keyword)
                ? keyword.Trim()
                : !string.IsNullOrWhiteSpace(search)
                    ? search.Trim()
                    : Request.Query["Keyword"].FirstOrDefault()?.Trim();

            if (User.IsInRole("Tenant"))
            {
                var input = new RentalSearchInput
                {
                    Keyword = effectiveKeyword, City = city, District = district,
                    MinPrice = minPrice, MaxPrice = maxPrice, MinArea = minArea, MaxArea = maxArea,
                    RoomType = roomType, Amenities = string.Join(",", amenities ?? []),
                    Sort = (sortBy ?? "newest").Replace('_', '-'), Page = page
                };
                input.Normalize();
                var values = input.RouteValues(); values["area"] = "Tenant";
                return RedirectToAction("Index", "RentalPost", values);
            }
            if (page < 1) page = 1;

            var publicListings = _context.RentalPosts.AsNoTracking().PublicListings();

            var query = RentalSearchQuery.Filter(publicListings
                .Include(p => p.Owner).Include(p => p.RoomImages), new RentalSearchCriteria
            {
                Keyword = effectiveKeyword, City = city, District = district,
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

            // LƯU SEARCH HISTORY — dùng method riêng
            await SaveSearchHistoryAsync(city, district, minPrice, maxPrice, roomType);

            // ViewBag
            ViewBag.Cities = await publicListings
                .Where(p => p.City != "")
                .Select(p => p.City).Distinct().OrderBy(c => c).ToListAsync();

            ViewBag.Districts = await publicListings
                .Where(p => p.District != "" && (string.IsNullOrEmpty(city) || p.City == city))
                .Select(p => p.District).Distinct().OrderBy(d => d).ToListAsync();

            ViewBag.Keyword = effectiveKeyword;
            ViewBag.Search = effectiveKeyword;
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

        private async Task SaveSearchHistoryAsync(
            string? city,
            string? district,
            decimal? minPrice,
            decimal? maxPrice,
            RoomType? roomType)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null) return;

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

        [HttpGet]
        public async Task<IActionResult> GetDistricts(string? city)
        {
            var districts = await _context.RentalPosts.AsNoTracking().PublicListings()
                .Where(p => p.District != "" && (string.IsNullOrEmpty(city) || p.City == city))
                .Select(p => p.District)
                .Distinct()
                .OrderBy(d => d)
                .ToListAsync();
            return Json(districts);
        }
    }
}
