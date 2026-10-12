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
    public class PostController : Controller
    {
        private readonly AppDbContext _context;

        public PostController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Post
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.RentalPosts.Include(r => r.ApprovedBy).Include(r => r.Owner);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Post/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rentalPost = await _context.RentalPosts
                .Include(r => r.ApprovedBy)
                .Include(r => r.Owner)
                .FirstOrDefaultAsync(m => m.RentalPostId == id);
            if (rentalPost == null)
            {
                return NotFound();
            }

            return View(rentalPost);
        }

        // GET: Post/Create
        public IActionResult Create()
        {
            ViewData["ApprovedById"] = new SelectList(_context.Users, "Id", "Id");
            ViewData["OwnerId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Post/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("RentalPostId,Title,Description,Price,Area,Address,City,District,Ward,MapLocation,AmenitiesJson,RoomType,RoomStatus,ApprovalStatus,ApprovalNote,ApprovedById,ApprovedAt,ElectricityUnitPrice,WaterUnitPrice,ServiceFee,AvailableFrom,CreatedAt,UpdatedAt,PostedAt,OwnerId")] RentalPost rentalPost)
        {
            if (ModelState.IsValid)
            {
                _context.Add(rentalPost);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["ApprovedById"] = new SelectList(_context.Users, "Id", "Id", rentalPost.ApprovedById);
            ViewData["OwnerId"] = new SelectList(_context.Users, "Id", "Id", rentalPost.OwnerId);
            return View(rentalPost);
        }

        // GET: Post/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rentalPost = await _context.RentalPosts.FindAsync(id);
            if (rentalPost == null)
            {
                return NotFound();
            }
            ViewData["ApprovedById"] = new SelectList(_context.Users, "Id", "Id", rentalPost.ApprovedById);
            ViewData["OwnerId"] = new SelectList(_context.Users, "Id", "Id", rentalPost.OwnerId);
            return View(rentalPost);
        }

        // POST: Post/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("RentalPostId,Title,Description,Price,Area,Address,City,District,Ward,MapLocation,AmenitiesJson,RoomType,RoomStatus,ApprovalStatus,ApprovalNote,ApprovedById,ApprovedAt,ElectricityUnitPrice,WaterUnitPrice,ServiceFee,AvailableFrom,CreatedAt,UpdatedAt,PostedAt,OwnerId")] RentalPost rentalPost)
        {
            if (id != rentalPost.RentalPostId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(rentalPost);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!RentalPostExists(rentalPost.RentalPostId))
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
            ViewData["ApprovedById"] = new SelectList(_context.Users, "Id", "Id", rentalPost.ApprovedById);
            ViewData["OwnerId"] = new SelectList(_context.Users, "Id", "Id", rentalPost.OwnerId);
            return View(rentalPost);
        }

        // GET: Post/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var rentalPost = await _context.RentalPosts
                .Include(r => r.ApprovedBy)
                .Include(r => r.Owner)
                .FirstOrDefaultAsync(m => m.RentalPostId == id);
            if (rentalPost == null)
            {
                return NotFound();
            }

            return View(rentalPost);
        }

        // POST: Post/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var rentalPost = await _context.RentalPosts.FindAsync(id);
            if (rentalPost != null)
            {
                _context.RentalPosts.Remove(rentalPost);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool RentalPostExists(int id)
        {
            return _context.RentalPosts.Any(e => e.RentalPostId == id);
        }
    }
}
