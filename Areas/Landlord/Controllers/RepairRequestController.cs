using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EZ_Rent.Data;
using EZ_Rent.Models;

namespace EZ_Rent.Areas_Landlord_Controllers
{
    [Area("Landlord")]
    public class RepairRequestController : Controller
    {
        private readonly AppDbContext _context;

        public RepairRequestController(AppDbContext context)
        {
            _context = context;
        }

        // GET: RepairRequest
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.RepairRequests.Include(r => r.RentalPost).Include(r => r.Tenant);
            return View(await appDbContext.ToListAsync());
        }

        // GET: RepairRequest/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var repairRequest = await _context.RepairRequests
                .Include(r => r.RentalPost)
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(m => m.RepairRequestId == id);
            if (repairRequest == null)
            {
                return NotFound();
            }

            return View(repairRequest);
        }

        // GET: RepairRequest/Create
        public IActionResult Create()
        {
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address");
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: RepairRequest/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RepairRequestId,RentalPostId,TenantId,ProblemType,Description,Priority,ImageUrl,Status,ResolutionNote,CreatedAt,UpdatedAt,ResolvedAt")] RepairRequest repairRequest)
        {
            if (ModelState.IsValid)
            {
                _context.Add(repairRequest);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", repairRequest.RentalPostId);
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id", repairRequest.TenantId);
            return View(repairRequest);
        }

        // GET: RepairRequest/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var repairRequest = await _context.RepairRequests.FindAsync(id);
            if (repairRequest == null)
            {
                return NotFound();
            }
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", repairRequest.RentalPostId);
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id", repairRequest.TenantId);
            return View(repairRequest);
        }

        // POST: RepairRequest/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RepairRequestId,RentalPostId,TenantId,ProblemType,Description,Priority,ImageUrl,Status,ResolutionNote,CreatedAt,UpdatedAt,ResolvedAt")] RepairRequest repairRequest)
        {
            if (id != repairRequest.RepairRequestId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(repairRequest);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RepairRequestExists(repairRequest.RepairRequestId))
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
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", repairRequest.RentalPostId);
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id", repairRequest.TenantId);
            return View(repairRequest);
        }

        // GET: RepairRequest/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var repairRequest = await _context.RepairRequests
                .Include(r => r.RentalPost)
                .Include(r => r.Tenant)
                .FirstOrDefaultAsync(m => m.RepairRequestId == id);
            if (repairRequest == null)
            {
                return NotFound();
            }

            return View(repairRequest);
        }

        // POST: RepairRequest/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var repairRequest = await _context.RepairRequests.FindAsync(id);
            if (repairRequest != null)
            {
                _context.RepairRequests.Remove(repairRequest);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RepairRequestExists(int id)
        {
            return _context.RepairRequests.Any(e => e.RepairRequestId == id);
        }
    }
}
