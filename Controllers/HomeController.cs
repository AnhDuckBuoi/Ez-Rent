using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;
using Microsoft.AspNetCore.Identity;

namespace EZ_Rent.Controllers;

public class HomeController : Controller
{
    private readonly RecommendationService _recommender;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _context;

    public HomeController(
        RecommendationService recommender,
        UserManager<ApplicationUser> userManager,
        AppDbContext context)
    {
        _recommender = recommender;
        _userManager = userManager;
        _context = context;
    }

    public async Task<IActionResult> Index(string? ui = null)
    {
        if (User.IsInRole("Tenant")) return RedirectToAction("Index", "Dashboard", new { area = "Tenant" });

        // Trang công khai giữ phần giới thiệu và gợi ý chung của nhóm.
        ViewBag.Recommendations = await _recommender.GetRecommendationsAsync("", 8);

        var publicPostsQuery = _context.RentalPosts
            .AsNoTracking()
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved
                     && p.RoomStatus != RoomStatus.Hidden);

        // ===== QUICK STATS =====
        ViewBag.TotalPosts = await publicPostsQuery.CountAsync();
        ViewBag.TotalUsers = await _context.Users.CountAsync();
        ViewBag.TotalLandlords = (await _userManager.GetUsersInRoleAsync("Landlord")).Count;
        ViewBag.TotalCities = await publicPostsQuery
            .Select(p => p.City)
            .Distinct()
            .CountAsync();

        // ===== SỐ LƯỢNG THEO LOẠI PHÒNG =====
        var typeGroups = await publicPostsQuery
            .GroupBy(p => p.RoomType)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToListAsync();
        ViewBag.RoomTypeCounts = typeGroups.ToDictionary(x => x.Type, x => x.Count);

        // ===== PHÒNG MỚI NHẤT (8 phòng cho lưới 4x2 cân đối) =====
        ViewBag.NewestPosts = await publicPostsQuery
            .Include(p => p.Owner)
            .Include(p => p.RoomImages)
            .OrderByDescending(p => p.PostedAt ?? p.CreatedAt)
            .Take(8)
            .ToListAsync();

        // ===== DANH MỤC KHU VỰC (top 4 thành phố có nhiều phòng + giá khởi điểm thật) =====
        var cityRows = await publicPostsQuery
            .Select(p => new { p.City, p.Price })
            .ToListAsync();

        ViewBag.TopCities = cityRows
            .GroupBy(p => p.City)
            .Select(g => new
            {
                City = g.Key,
                Count = g.Count(),
                MinPrice = g.Min(p => p.Price)
            })
            .OrderByDescending(x => x.Count)
            .Take(4)
            .ToList();

        if (string.Equals(ui, "legacy", StringComparison.OrdinalIgnoreCase))
        {
            return View("Index.Legacy");
        }

        return View();
    }

    public IActionResult About()
    {
        return View();
    }

    public IActionResult Guide()
    {
        return View();
    }

    public IActionResult Help()
    {
        return View();
    }

    public IActionResult Terms()
    {
        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
