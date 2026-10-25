using EZ_Rent.Data;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EZ_Rent.Services;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class InvoiceController : TenantControllerBase
{
    private readonly RentalPrivateFileStore files;
    public InvoiceController(AppDbContext context, RentalPrivateFileStore files) : base(context) => this.files = files;

    private IQueryable<Invoice> MyInvoices => Context.Invoices.AsNoTracking()
        .Where(i => i.RentalContract.TenantId == CurrentUserId)
        .Include(i => i.RentalContract).ThenInclude(c => c.RentalPost);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] InvoiceListInput input)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new InvoiceListModel(input, [], 0, 1));
        }
        if (input.ContractId.HasValue && !await Context.RentalContracts.AnyAsync(c =>
            c.RentalContractId == input.ContractId && c.TenantId == CurrentUserId)) return NotFound();
        var query = MyInvoices;
        if (input.ContractId.HasValue) query = query.Where(i => i.RentalContractId == input.ContractId);
        if (input.Status.HasValue) query = query.Where(i => i.PaymentStatus == input.Status);
        var total = await query.CountAsync();
        var pages = TenantRentalPresentation.Pages(total);
        input.Page = Math.Min(input.Page, pages);
        var items = await query.OrderByDescending(i => i.BillingPeriod).ThenByDescending(i => i.InvoiceId)
            .Skip((input.Page - 1) * TenantRentalPresentation.PageSize).Take(TenantRentalPresentation.PageSize).ToListAsync();
        return View(new InvoiceListModel(input, items, total, pages));
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int? id)
    {
        var invoice = await MyInvoices.Include(i => i.MeterReading).FirstOrDefaultAsync(i => i.InvoiceId == id);
        if (invoice == null) return NotFound();
        // Do not show another room/period if a legacy record has an inconsistent meter reference.
        if (invoice.MeterReading != null && (invoice.MeterReading.RentalPostId != invoice.RentalContract.RentalPostId ||
            invoice.MeterReading.Period != invoice.BillingPeriod)) invoice.MeterReading = null;
        var evidence = await Context.InvoiceMeterEvidence.AsNoTracking().SingleOrDefaultAsync(e => e.InvoiceId == invoice.InvoiceId && e.TenantId == CurrentUserId &&
            e.RentalPostId == invoice.RentalContract.RentalPostId && e.Period == invoice.BillingPeriod);
        return View(new InvoiceDetailsModel(invoice, evidence));
    }

    [HttpGet]
    public async Task<IActionResult> MeterImage(int id, string kind, bool download = false)
    {
        if (kind is not ("electric" or "water")) return NotFound();
        var evidence = await Context.InvoiceMeterEvidence.AsNoTracking().SingleOrDefaultAsync(e => e.InvoiceId == id &&
            e.Invoice.RentalContract.TenantId == CurrentUserId && e.TenantId == CurrentUserId && e.RentalPostId == e.Invoice.RentalContract.RentalPostId &&
            e.Period == e.Invoice.BillingPeriod);
        if (evidence == null) return NotFound();
        var key = kind == "electric" ? evidence.ElectricStorageKey : evidence.WaterStorageKey;
        var type = kind == "electric" ? evidence.ElectricContentType : evidence.WaterContentType;
        if (key == null || type is not ("image/png" or "image/jpeg")) return NotFound();
        var stream = files.OpenRead(key);
        if (stream == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'self'; sandbox";
        return download ? File(stream, type, $"cong-to-{kind}-{id}{Path.GetExtension(key)}") : File(stream, type);
    }

    // Amounts and PaidAt can only be changed by the future server payment workflow.
}
