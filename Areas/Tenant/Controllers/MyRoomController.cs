using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyRoomController(AppDbContext db) : TenantControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] int? contractId)
    {
        if (!ModelState.IsValid || contractId is <= 0) return BadRequest();
        var contracts = await Context.RentalContracts.AsNoTracking()
            .Where(c => c.TenantId == CurrentUserId && c.Status == ContractStatus.Active)
            .Include(c => c.RentalPost).ThenInclude(p => p.RoomImages).Include(c => c.Landlord)
            .OrderByDescending(c => c.StartDate).ThenByDescending(c => c.RentalContractId).ToListAsync();
        var selected = contractId.HasValue ? contracts.Find(c => c.RentalContractId == contractId) : contracts.FirstOrDefault();
        if (contractId.HasValue && selected == null) return NotFound();
        if (selected == null) return View(new MyRoomModel());

        // Always retain ownership in each query, even after resolving the selected contract.
        var invoices = Context.Invoices.AsNoTracking().Where(i => i.RentalContract.TenantId == CurrentUserId
            && i.RentalContractId == selected.RentalContractId);
        var amounts = await invoices.Where(i => i.PaymentStatus == PaymentStatus.Unpaid ||
            i.PaymentStatus == PaymentStatus.Overdue || i.PaymentStatus == PaymentStatus.AwaitingConfirmation)
            .Select(i => i.TotalAmount).ToListAsync();
        var repairs = Context.RepairRequests.AsNoTracking()
            .Where(r => r.TenantId == CurrentUserId && r.RentalPostId == selected.RentalPostId);
        return View(new MyRoomModel
        {
            Contracts = contracts,
            Selected = selected,
            UnpaidCount = amounts.Count,
            UnpaidTotal = amounts.Sum(),
            Invoices = await invoices.OrderByDescending(i => i.BillingPeriod).ThenByDescending(i => i.InvoiceId).Take(3).ToListAsync(),
            OpenRepairCount = await repairs.CountAsync(r => r.Status == RepairRequestStatus.Pending || r.Status == RepairRequestStatus.InProgress),
            Repairs = await repairs.OrderByDescending(r => r.CreatedAt).ThenByDescending(r => r.RepairRequestId).Take(3).ToListAsync()
        });
    }
}
