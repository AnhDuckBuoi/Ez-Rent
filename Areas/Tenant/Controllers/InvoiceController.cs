using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
public class InvoiceController : TenantControllerBase
{
    public InvoiceController(AppDbContext context) : base(context) { }

    private IQueryable<Invoice> MyInvoices => Context.Invoices.AsNoTracking()
        .Where(i => i.RentalContract.TenantId == CurrentUserId)
        .Include(i => i.RentalContract).ThenInclude(c => c.RentalPost).Include(i => i.MeterReading);

    [HttpGet]
    public async Task<IActionResult> Index() => View(await MyInvoices.ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int? id)
    {
        var invoice = await MyInvoices.FirstOrDefaultAsync(i => i.InvoiceId == id);
        return invoice == null ? NotFound() : View(invoice);
    }

    // Amounts and PaidAt can only be changed by the future server payment workflow.
}
