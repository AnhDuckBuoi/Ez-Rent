using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
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
        var now = DateTime.UtcNow;

        // ===== YÊU THÍCH =====
        ViewBag.TotalFavorites = await _context.Favorites
            .CountAsync(f => f.UserId == userId);

        // ===== LỊCH HẸN =====
        ViewBag.UpcomingViewings = await _context.ViewingAppointments
            .CountAsync(v => v.TenantId == userId
                          && v.ViewingDate >= now
                          && v.Status != ViewingAppointmentStatus.Cancelled);
        ViewBag.PendingViewings = await _context.ViewingAppointments
            .CountAsync(v => v.TenantId == userId
                          && v.Status == ViewingAppointmentStatus.Pending);

        // ===== HỢP ĐỒNG =====
        ViewBag.ActiveContracts = await _context.RentalContracts
            .CountAsync(c => c.TenantId == userId && c.Status == ContractStatus.Active);
        ViewBag.PendingContracts = await _context.RentalContracts
            .CountAsync(c => c.TenantId == userId && c.Status == ContractStatus.PendingTenant);

        // ===== HÓA ĐƠN =====
        ViewBag.UnpaidInvoices = await _context.Invoices
            .CountAsync(i => i.RentalContract.TenantId == userId
                          && i.PaymentStatus != PaymentStatus.Paid);
        ViewBag.OverdueInvoices = await _context.Invoices
            .CountAsync(i => i.RentalContract.TenantId == userId
                          && i.DueDate < now
                          && i.PaymentStatus != PaymentStatus.Paid);

        var currentPeriod = now.ToString("yyyy-MM");
        ViewBag.MonthDue = await _context.Invoices
            .Where(i => i.RentalContract.TenantId == userId
                     && i.BillingPeriod == currentPeriod
                     && i.PaymentStatus != PaymentStatus.Paid)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0;

        // ===== YÊU CẦU SỬA CHỮA =====
        ViewBag.OpenRepairs = await _context.RepairRequests
            .CountAsync(r => r.TenantId == userId
                          && r.Status != RepairRequestStatus.Resolved);

        // ===== DỮ LIỆU GẦN ĐÂY =====
        ViewBag.RecentViewings = await _context.ViewingAppointments
            .Include(v => v.RentalPost)
            .Where(v => v.TenantId == userId
                     && v.Status != ViewingAppointmentStatus.Completed
                     && v.Status != ViewingAppointmentStatus.Cancelled)
            .OrderBy(v => v.ViewingDate)
            .Take(5)
            .ToListAsync();

        ViewBag.UnpaidInvoiceList = await _context.Invoices
            .Include(i => i.RentalContract)
                .ThenInclude(c => c.RentalPost)
            .Where(i => i.RentalContract.TenantId == userId
                     && i.PaymentStatus != PaymentStatus.Paid)
            .OrderBy(i => i.DueDate)
            .Take(5)
            .ToListAsync();

        // Hợp đồng sắp hết hạn (trong 30 ngày)
        var soon = now.AddDays(30);
        ViewBag.ExpiringContracts = await _context.RentalContracts
            .Include(c => c.RentalPost)
            .Where(c => c.TenantId == userId
                     && c.Status == ContractStatus.Active
                     && c.EndDate <= soon
                     && c.EndDate >= now)
            .OrderBy(c => c.EndDate)
            .Take(3)
            .ToListAsync();

        return View();
    }
}