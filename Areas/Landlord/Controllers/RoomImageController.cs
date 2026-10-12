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
    public class RoomImageController : Controller
    {
        private readonly AppDbContext _context;

        public RoomImageController(AppDbContext context)
        {
            _context = context;
        }

        // GET: RoomImage
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.RoomImages.Include(r => r.RentalPost);
            return View(await appDbContext.ToListAsync());
        }

        // GET: RoomImage/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var roomImage = await _context.RoomImages
                .Include(r => r.RentalPost)
                .FirstOrDefaultAsync(m => m.RoomImageId == id);
            if (roomImage == null)
            {
                return NotFound();
            }

            return View(roomImage);
        }

        // GET: RoomImage/Create
        public IActionResult Create()
        {
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address");
            return View();
        }

        // POST: RoomImage/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RoomImageId,MediaUrl,MediaType,IsPrimary,DisplayOrder,CreatedAt,RentalPostId")] RoomImage roomImage)
        {
            if (ModelState.IsValid)
            {
                _context.Add(roomImage);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", roomImage.RentalPostId);
            return View(roomImage);
        }

        // GET: RoomImage/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var roomImage = await _context.RoomImages.FindAsync(id);
            if (roomImage == null)
            {
                return NotFound();
            }
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", roomImage.RentalPostId);
            return View(roomImage);
        }

        // POST: RoomImage/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RoomImageId,MediaUrl,MediaType,IsPrimary,DisplayOrder,CreatedAt,RentalPostId")] RoomImage roomImage)
        {
            if (id != roomImage.RoomImageId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(roomImage);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RoomImageExists(roomImage.RoomImageId))
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
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", roomImage.RentalPostId);
            return View(roomImage);
        }

        // GET: RoomImage/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var roomImage = await _context.RoomImages
                .Include(r => r.RentalPost)
                .FirstOrDefaultAsync(m => m.RoomImageId == id);
            if (roomImage == null)
            {
                return NotFound();
            }

            return View(roomImage);
        }

        // POST: RoomImage/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var roomImage = await _context.RoomImages.FindAsync(id);
            if (roomImage != null)
            {
                _context.RoomImages.Remove(roomImage);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RoomImageExists(int id)
        {
            return _context.RoomImages.Any(e => e.RoomImageId == id);
        }
    }
}
