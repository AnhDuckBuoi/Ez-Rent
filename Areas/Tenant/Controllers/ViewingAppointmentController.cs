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
    public class ViewingAppointmentController : Controller
    {
        private readonly AppDbContext _context;

        public ViewingAppointmentController(AppDbContext context)
        {
            _context = context;
        }

        // GET: ViewingAppointment
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.ViewingAppointments.Include(v => v.RentalPost).Include(v => v.Tenant);
            return View(await appDbContext.ToListAsync());
        }

        // GET: ViewingAppointment/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var viewingAppointment = await _context.ViewingAppointments
                .Include(v => v.RentalPost)
                .Include(v => v.Tenant)
                .FirstOrDefaultAsync(m => m.ViewingAppointmentId == id);
            if (viewingAppointment == null)
            {
                return NotFound();
            }

            return View(viewingAppointment);
        }

        // GET: ViewingAppointment/Create
        public IActionResult Create()
        {
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address");
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: ViewingAppointment/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ViewingAppointmentId,RentalPostId,TenantId,ViewingDate,Note,LandlordNote,Status,CreatedAt,UpdatedAt")] ViewingAppointment viewingAppointment)
        {
            if (ModelState.IsValid)
            {
                _context.Add(viewingAppointment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", viewingAppointment.RentalPostId);
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id", viewingAppointment.TenantId);
            return View(viewingAppointment);
        }

        // GET: ViewingAppointment/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var viewingAppointment = await _context.ViewingAppointments.FindAsync(id);
            if (viewingAppointment == null)
            {
                return NotFound();
            }
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", viewingAppointment.RentalPostId);
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id", viewingAppointment.TenantId);
            return View(viewingAppointment);
        }

        // POST: ViewingAppointment/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ViewingAppointmentId,RentalPostId,TenantId,ViewingDate,Note,LandlordNote,Status,CreatedAt,UpdatedAt")] ViewingAppointment viewingAppointment)
        {
            if (id != viewingAppointment.ViewingAppointmentId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(viewingAppointment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ViewingAppointmentExists(viewingAppointment.ViewingAppointmentId))
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
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", viewingAppointment.RentalPostId);
            ViewData["TenantId"] = new SelectList(_context.Users, "Id", "Id", viewingAppointment.TenantId);
            return View(viewingAppointment);
        }

        // GET: ViewingAppointment/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var viewingAppointment = await _context.ViewingAppointments
                .Include(v => v.RentalPost)
                .Include(v => v.Tenant)
                .FirstOrDefaultAsync(m => m.ViewingAppointmentId == id);
            if (viewingAppointment == null)
            {
                return NotFound();
            }

            return View(viewingAppointment);
        }

        // POST: ViewingAppointment/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var viewingAppointment = await _context.ViewingAppointments.FindAsync(id);
            if (viewingAppointment != null)
            {
                _context.ViewingAppointments.Remove(viewingAppointment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool ViewingAppointmentExists(int id)
        {
            return _context.ViewingAppointments.Any(e => e.ViewingAppointmentId == id);
        }
    }
}
