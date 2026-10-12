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
    public class FavoriteController : Controller
    {
        private readonly AppDbContext _context;

        public FavoriteController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Favorite
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.Favorites.Include(f => f.RentalPost).Include(f => f.User);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Favorite/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var favorite = await _context.Favorites
                .Include(f => f.RentalPost)
                .Include(f => f.User)
                .FirstOrDefaultAsync(m => m.FavoriteId == id);
            if (favorite == null)
            {
                return NotFound();
            }

            return View(favorite);
        }

        // GET: Favorite/Create
        public IActionResult Create()
        {
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address");
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id");
            return View();
        }

        // POST: Favorite/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FavoriteId,CreatedAt,UserId,RentalPostId")] Favorite favorite)
        {
            if (ModelState.IsValid)
            {
                _context.Add(favorite);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", favorite.RentalPostId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", favorite.UserId);
            return View(favorite);
        }

        // GET: Favorite/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var favorite = await _context.Favorites.FindAsync(id);
            if (favorite == null)
            {
                return NotFound();
            }
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", favorite.RentalPostId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", favorite.UserId);
            return View(favorite);
        }

        // POST: Favorite/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("FavoriteId,CreatedAt,UserId,RentalPostId")] Favorite favorite)
        {
            if (id != favorite.FavoriteId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(favorite);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!FavoriteExists(favorite.FavoriteId))
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
            ViewData["RentalPostId"] = new SelectList(_context.RentalPosts, "RentalPostId", "Address", favorite.RentalPostId);
            ViewData["UserId"] = new SelectList(_context.Users, "Id", "Id", favorite.UserId);
            return View(favorite);
        }

        // GET: Favorite/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var favorite = await _context.Favorites
                .Include(f => f.RentalPost)
                .Include(f => f.User)
                .FirstOrDefaultAsync(m => m.FavoriteId == id);
            if (favorite == null)
            {
                return NotFound();
            }

            return View(favorite);
        }

        // POST: Favorite/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var favorite = await _context.Favorites.FindAsync(id);
            if (favorite != null)
            {
                _context.Favorites.Remove(favorite);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool FavoriteExists(int id)
        {
            return _context.Favorites.Any(e => e.FavoriteId == id);
        }
    }
}
