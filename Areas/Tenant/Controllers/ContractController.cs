using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EZ_Rent.Data;
using EZ_Rent.Models;

namespace EZ_Rent.Areas_Tenant_Controllers
{
    [Area("Tenant")]
    public class ContractController : Controller
    {
        private readonly AppDbContext _context;

        public ContractController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Contract
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.RentalContracts.Include(r => r.Landlord).Include(r => r.RentalPost).Include(r => r.Tenant);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Contract/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rentalContract = await _context.RentalContracts
                .Include(r => r.Landlord)
                .Include(r => r.RentalPost)
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(m => m.RentalContractId == id);
            if (rentalContract == null)
            {
                return NotFound();
            }

            return View(rentalContract);
        }

        // GET: Contract/Create
        public IActionResult Create()
        {
            ViewData["LandlordId"] = new SelectList(_context.Users, "Id", "Id");
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address");
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Contract/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RentalContractId,RentalPostId,TenantId,LandlordId,StartDate,EndDate,MonthlyRent,DepositAmount,AgreedElectricityPrice,AgreedWaterPrice,Terms,Status,TenantConfirmedAt,LandlordConfirmedAt,CreatedAt,UpdatedAt")] RentalContract rentalContract)
        {
            if (ModelState.IsValid)
            {
                _context.Add(rentalContract);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["LandlordId"] = new SelectList(_context.Users, "Id", "Id", rentalContract.LandlordId);
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", rentalContract.RentalPostId);
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id", rentalContract.TenantId);
            return View(rentalContract);
        }

        // GET: Contract/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rentalContract = await _context.RentalContracts.FindAsync(id);
            if (rentalContract == null)
            {
                return NotFound();
            }
            ViewData["LandlordId"] = new SelectList(_context.Users, "Id", "Id", rentalContract.LandlordId);
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", rentalContract.RentalPostId);
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id", rentalContract.TenantId);
            return View(rentalContract);
        }

        // POST: Contract/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RentalContractId,RentalPostId,TenantId,LandlordId,StartDate,EndDate,MonthlyRent,DepositAmount,AgreedElectricityPrice,AgreedWaterPrice,Terms,Status,TenantConfirmedAt,LandlordConfirmedAt,CreatedAt,UpdatedAt")] RentalContract rentalContract)
        {
            if (id != rentalContract.RentalContractId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(rentalContract);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RentalContractExists(rentalContract.RentalContractId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["LandlordId"] = new SelectList(_context.Users, "Id", "Id", rentalContract.LandlordId);
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", rentalContract.RentalPostId);
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id", rentalContract.TenantId);
            return View(rentalContract);
        }

        // GET: Contract/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rentalContract = await _context.RentalContracts
                .Include(r => r.Landlord)
                .Include(r => r.RentalPost)
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(m => m.RentalContractId == id);
            if (rentalContract == null)
            {
                return NotFound();
            }

            return View(rentalContract);
        }

        // POST: Contract/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rentalContract = await _context.RentalContracts.FindAsync(id);
            if (rentalContract != null)
            {
                _context.RentalContracts.Remove(rentalContract);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RentalContractExists(int id)
        {
            return _context.RentalContracts.Any(e => e.RentalContractId == id);
        }
    }
}
