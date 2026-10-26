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

    public async Task<IActionResult> Index()
    {
        if (User.IsInRole("Tenant")) return RedirectToAction("Index", "Dashboard", new { area = "Tenant" });
        // Trang công khai giữ phần giới thiệu và gợi ý chung của nhóm.
        ViewBag.Recommendations = await _recommender.GetRecommendationsAsync("", 6);

        // ===== QUICK STATS =====
        ViewBag.TotalPosts   = await _context.RentalPosts
            .CountAsync(p => p.ApprovalStatus == ApprovalStatus.Approved
                          && p.RoomStatus != RoomStatus.Hidden);

        ViewBag.TotalUsers   = await _context.Users.CountAsync();

        ViewBag.TotalLandlords = (await _userManager.GetUsersInRoleAsync("Landlord")).Count;

        ViewBag.TotalCities  = await _context.RentalPosts
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
            .Select(p => p.City)
            .Distinct()
            .CountAsync();

        // ===== PHÒNG MỚI NHẤT (8 phòng) =====
        ViewBag.NewestPosts = await _context.RentalPosts
            .Include(p => p.Owner)
            .Include(p => p.RoomImages)
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved
                     && p.RoomStatus != RoomStatus.Hidden)
            .OrderByDescending(p => p.PostedAt ?? p.CreatedAt)
            .Take(8)
            .ToListAsync();

        // ===== DANH MỤC KHU VỰC (top 4 thành phố có nhiều phòng) =====
        ViewBag.TopCities = await _context.RentalPosts
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved
                     && p.RoomStatus != RoomStatus.Hidden)
            .GroupBy(p => p.City)
            .Select(g => new { City = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .Take(4)
            .ToListAsync();

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
