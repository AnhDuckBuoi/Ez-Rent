using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Controllers
{
    [Authorize]
    public class MyController : Controller
    {
        private readonly AppDbContext _context;
        private readonly UserManager<Models.ApplicationUser> _userManager;

        public MyController(
            AppDbContext context,
            UserManager<Models.ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // GET: /My/Posts — Landlord
        [Authorize(Roles = "Landlord")]
        public async Task<IActionResult> Posts()
        {
            var userId = _userManager.GetUserId(User);

            var posts = await _context.RentalPosts
                .Include(p => p.RoomImages)
                .Where(p => p.OwnerId == userId
                         && p.ApprovalStatus != ApprovalStatus.Removed)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return View(posts);
        }

        // GET: /My/Contracts — Tenant + Landlord
        public async Task<IActionResult> Contracts()
        {
            var userId = _userManager.GetUserId(User);

            IQueryable<Models.RentalContract> query = _context.RentalContracts
                .Include(c => c.RentalPost)
                .Include(c => c.Tenant)
                .Include(c => c.Landlord);

            if (User.IsInRole("Landlord"))
                query = query.Where(c => c.LandlordId == userId);
            else if (User.IsInRole("Tenant"))
                query = query.Where(c => c.TenantId == userId);
            else
                return Forbid();

            var contracts = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            return View(contracts);
        }

        // GET: /My/Invoices — Tenant + Landlord
        public async Task<IActionResult> Invoices()
        {
            var userId = _userManager.GetUserId(User);

            IQueryable<Models.Invoice> query = _context.Invoices
                .Include(i => i.RentalContract)
                    .ThenInclude(c => c.RentalPost);

            if (User.IsInRole("Landlord"))
                query = query.Where(i => i.RentalContract.LandlordId == userId);
            else if (User.IsInRole("Tenant"))
                query = query.Where(i => i.RentalContract.TenantId == userId);
            else
                return Forbid();

            var invoices = await query
                .OrderByDescending(i => i.IssueDate)
                .ToListAsync();

            return View(invoices);
        }
    }
}