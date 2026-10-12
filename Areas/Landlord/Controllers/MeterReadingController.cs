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
    public class MeterReadingController : Controller
    {
        private readonly AppDbContext _context;

        public MeterReadingController(AppDbContext context)
        {
            _context = context;
        }

        // GET: MeterReading
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.MeterReadings.Include(m => m.RecordedBy).Include(m => m.RentalPost);
            return View(await appDbContext.ToListAsync());
        }

        // GET: MeterReading/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var meterReading = await _context.MeterReadings
                .Include(m => m.RecordedBy)
                .Include(m => m.RentalPost)
                .FirstOrDefaultAsync(m => m.MeterReadingId == id);
            if (meterReading == null)
            {
                return NotFound();
            }

            return View(meterReading);
        }

        // GET: MeterReading/Create
        public IActionResult Create()
        {
            ViewData["RecordedById"] = new SelectList(_context.Users, "Id", "Id");
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address");
            return View();
        }

        // POST: MeterReading/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("MeterReadingId,RentalPostId,Period,ReadingDate,PrevElectricIndex,CurrElectricIndex,ElectricUsage,ElectricMeterImageUrl,PrevWaterIndex,CurrWaterIndex,WaterUsage,WaterMeterImageUrl,RecordedById,Note")] MeterReading meterReading)
        {
            if (ModelState.IsValid)
            {
                _context.Add(meterReading);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RecordedById"] = new SelectList(_context.Users, "Id", "Id", meterReading.RecordedById);
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", meterReading.RentalPostId);
            return View(meterReading);
        }

        // GET: MeterReading/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var meterReading = await _context.MeterReadings.FindAsync(id);
            if (meterReading == null)
            {
                return NotFound();
            }
            ViewData["RecordedById"] = new SelectList(_context.Users, "Id", "Id", meterReading.RecordedById);
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", meterReading.RentalPostId);
            return View(meterReading);
        }

        // POST: MeterReading/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("MeterReadingId,RentalPostId,Period,ReadingDate,PrevElectricIndex,CurrElectricIndex,ElectricUsage,ElectricMeterImageUrl,PrevWaterIndex,CurrWaterIndex,WaterUsage,WaterMeterImageUrl,RecordedById,Note")] MeterReading meterReading)
        {
            if (id != meterReading.MeterReadingId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(meterReading);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MeterReadingExists(meterReading.MeterReadingId))
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
            ViewData["RecordedById"] = new SelectList(_context.Users, "Id", "Id", meterReading.RecordedById);
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", meterReading.RentalPostId);
            return View(meterReading);
        }

        // GET: MeterReading/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var meterReading = await _context.MeterReadings
                .Include(m => m.RecordedBy)
                .Include(m => m.RentalPost)
                .FirstOrDefaultAsync(m => m.MeterReadingId == id);
            if (meterReading == null)
            {
                return NotFound();
            }

            return View(meterReading);
        }

        // POST: MeterReading/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var meterReading = await _context.MeterReadings.FindAsync(id);
            if (meterReading != null)
            {
                _context.MeterReadings.Remove(meterReading);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MeterReadingExists(int id)
        {
            return _context.MeterReadings.Any(e => e.MeterReadingId == id);
        }
    }
}
