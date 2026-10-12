using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

[Area("Landlord")]
[Authorize(Roles = "Landlord")]
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
        var userId = _userManager.GetUserId(User);

        // ===== PHÒNG =====
        ViewBag.TotalPosts = await _context.RentalPosts
            .CountAsync(p => p.OwnerId == userId);
        ViewBag.AvailablePosts = await _context.RentalPosts
            .CountAsync(p => p.OwnerId == userId && p.RoomStatus == RoomStatus.Available);
        ViewBag.RentedPosts = await _context.RentalPosts
            .CountAsync(p => p.OwnerId == userId && p.RoomStatus == RoomStatus.Rented);
        ViewBag.PendingApproval = await _context.RentalPosts
            .CountAsync(p => p.OwnerId == userId && p.ApprovalStatus == ApprovalStatus.Pending);

        // ===== LỊCH HẸN =====
        ViewBag.PendingViewings = await _context.ViewingAppointments
            .CountAsync(v => v.RentalPost.OwnerId == userId
                          && v.Status == ViewingAppointmentStatus.Pending);
        ViewBag.ConfirmedViewings = await _context.ViewingAppointments
            .CountAsync(v => v.RentalPost.OwnerId == userId
                          && v.Status == ViewingAppointmentStatus.Confirmed);

        // ===== HỢP ĐỒNG & HÓA ĐƠN =====
        ViewBag.ActiveContracts = await _context.RentalContracts
            .CountAsync(c => c.LandlordId == userId && c.Status == ContractStatus.Active);
        ViewBag.UnpaidInvoices = await _context.Invoices
            .CountAsync(i => i.RentalContract.LandlordId == userId
                          && i.PaymentStatus != PaymentStatus.Paid);
        ViewBag.OverdueInvoices = await _context.Invoices
            .CountAsync(i => i.RentalContract.LandlordId == userId
                          && i.DueDate < DateTime.UtcNow
                          && i.PaymentStatus != PaymentStatus.Paid);

        // ===== SỰ CỐ =====
        ViewBag.OpenRepairs = await _context.RepairRequests
            .CountAsync(r => r.RentalPost.OwnerId == userId
                          && r.Status != RepairRequestStatus.Resolved);

        // ===== DỮ LIỆU GẦN ĐÂY =====
        ViewBag.RecentViewings = await _context.ViewingAppointments
            .Include(v => v.RentalPost)
            .Include(v => v.Tenant)
            .Where(v => v.RentalPost.OwnerId == userId
                     && v.Status == ViewingAppointmentStatus.Pending)
            .OrderBy(v => v.ViewingDate)
            .Take(5)
            .ToListAsync();

        ViewBag.RecentRepairs = await _context.RepairRequests
            .Include(r => r.RentalPost)
            .Include(r => r.Tenant)
            .Where(r => r.RentalPost.OwnerId == userId
                     && r.Status != RepairRequestStatus.Resolved)
            .OrderByDescending(r => r.Priority)
            .ThenBy(r => r.CreatedAt)
            .Take(5)
            .ToListAsync();

        // ===== TỔNG DOANH THU THÁNG NÀY =====
        var currentPeriod = DateTime.UtcNow.ToString("yyyy-MM");
        ViewBag.MonthRevenue = await _context.Invoices
            .Where(i => i.RentalContract.LandlordId == userId
                     && i.BillingPeriod == currentPeriod
                     && i.PaymentStatus == PaymentStatus.Paid)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

        return View();
    }
}