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
    public class InvoiceController : Controller
    {
        private readonly AppDbContext _context;

        public InvoiceController(AppDbContext context)
        {
            _context = context;
        }

        // GET: Invoice
        public async Task<IActionResult> Index()
        {
            var appDbContext = _context.Invoices.Include(i => i.MeterReading).Include(i => i.RentalContract);
            return View(await appDbContext.ToListAsync());
        }

        // GET: Invoice/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.MeterReading)
                .Include(i => i.RentalContract)
                .FirstOrDefaultAsync(m => m.InvoiceId == id);
            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        // GET: Invoice/Create
        public IActionResult Create()
        {
            ViewData["MeterReadingId"] = new SelectList(_context.MeterReadings, "MeterReadingId", "Period");
            ViewData["RentalContractId"] = new SelectList(_context.RentalContracts, "RentalContractId", "RentalContractId");
            return View();
        }

        // POST: Invoice/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("InvoiceId,RentalContractId,MeterReadingId,BillingPeriod,RoomRentAmount,ElectricUsage,ElectricAmount,WaterUsage,WaterAmount,ServiceFeeAmount,AdjustmentAmount,TotalAmount,IssueDate,DueDate,PaymentStatus,VietQRContent,TenantConfirmedAt,PaidAt,Note,CreatedAt,UpdatedAt")] Invoice invoice)
        {
            if (ModelState.IsValid)
            {
                _context.Add(invoice);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["MeterReadingId"] = new SelectList(_context.MeterReadings, "MeterReadingId", "Period", invoice.MeterReadingId);
            ViewData["RentalContractId"] = new SelectList(_context.RentalContracts, "RentalContractId", "RentalContractId", invoice.RentalContractId);
            return View(invoice);
        }

        // GET: Invoice/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice == null)
            {
                return NotFound();
            }
            ViewData["MeterReadingId"] = new SelectList(_context.MeterReadings, "MeterReadingId", "Period", invoice.MeterReadingId);
            ViewData["RentalContractId"] = new SelectList(_context.RentalContracts, "RentalContractId", "RentalContractId", invoice.RentalContractId);
            return View(invoice);
        }

        // POST: Invoice/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("InvoiceId,RentalContractId,MeterReadingId,BillingPeriod,RoomRentAmount,ElectricUsage,ElectricAmount,WaterUsage,WaterAmount,ServiceFeeAmount,AdjustmentAmount,TotalAmount,IssueDate,DueDate,PaymentStatus,VietQRContent,TenantConfirmedAt,PaidAt,Note,CreatedAt,UpdatedAt")] Invoice invoice)
        {
            if (id != invoice.InvoiceId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(invoice);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(invoice.InvoiceId))
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
            ViewData["MeterReadingId"] = new SelectList(_context.MeterReadings, "MeterReadingId", "Period", invoice.MeterReadingId);
            ViewData["RentalContractId"] = new SelectList(_context.RentalContracts, "RentalContractId", "RentalContractId", invoice.RentalContractId);
            return View(invoice);
        }

        // GET: Invoice/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var invoice = await _context.Invoices
                .Include(i => i.MeterReading)
                .Include(i => i.RentalContract)
                .FirstOrDefaultAsync(m => m.InvoiceId == id);
            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        // POST: Invoice/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var invoice = await _context.Invoices.FindAsync(id);
            if (invoice != null)
            {
                _context.Invoices.Remove(invoice);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceExists(int id)
        {
            return _context.Invoices.Any(e => e.InvoiceId == id);
        }
    }
}
