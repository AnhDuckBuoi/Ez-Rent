using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly AppDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public DashboardController(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        // ===== THỐNG KÊ =====
        ViewBag.TotalUsers = await _context.Users.CountAsync();
        ViewBag.TotalLandlords = (await _userManager.GetUsersInRoleAsync("Landlord")).Count;
        ViewBag.TotalTenants = (await _userManager.GetUsersInRoleAsync("Tenant")).Count;
        ViewBag.LockedUsers = await _context.Users.CountAsync(u => u.Status == UserStatus.Locked);

        ViewBag.TotalPosts = await _context.RentalPosts.CountAsync();
        ViewBag.PendingPosts = await _context.RentalPosts
            .CountAsync(p => p.ApprovalStatus == ApprovalStatus.Pending);
        ViewBag.ApprovedPosts = await _context.RentalPosts
            .CountAsync(p => p.ApprovalStatus == ApprovalStatus.Approved);
        ViewBag.RejectedPosts = await _context.RentalPosts
            .CountAsync(p => p.ApprovalStatus == ApprovalStatus.Rejected);

        ViewBag.TotalContracts = await _context.RentalContracts.CountAsync();
        ViewBag.ActiveContracts = await _context.RentalContracts
            .CountAsync(c => c.Status == ContractStatus.Active);

        ViewBag.OpenRepairs = await _context.RepairRequests
            .CountAsync(r => r.Status != RepairRequestStatus.Resolved);
        ViewBag.UrgentRepairs = await _context.RepairRequests
            .CountAsync(r => r.Status != RepairRequestStatus.Resolved
                          && r.Priority == RepairPriority.Urgent);

        // ===== DỮ LIỆU GẦN ĐÂY =====
        ViewBag.RecentPendingPosts = await _context.RentalPosts
            .Include(p => p.Owner)
            .Where(p => p.ApprovalStatus == ApprovalStatus.Pending)
            .OrderByDescending(p => p.CreatedAt)
            .Take(5)
            .ToListAsync();

        ViewBag.RecentRepairs = await _context.RepairRequests
            .Include(r => r.Tenant)
            .Include(r => r.RentalPost)
            .Where(r => r.Status != RepairRequestStatus.Resolved)
            .OrderByDescending(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        return View();
    }
}