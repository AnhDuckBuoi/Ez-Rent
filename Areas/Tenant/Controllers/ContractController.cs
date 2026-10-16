using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
public class ContractController : TenantControllerBase
{
    public ContractController(AppDbContext context) : base(context) { }

    private IQueryable<RentalContract> MyContracts => Context.RentalContracts.AsNoTracking()
        .Where(c => c.TenantId == CurrentUserId)
        .Include(c => c.RentalPost).Include(c => c.Tenant).Include(c => c.Landlord);

    [HttpGet]
    public async Task<IActionResult> Index() => View(await MyContracts.ToListAsync());

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int? id)
    {
        var contract = await MyContracts.FirstOrDefaultAsync(c => c.RentalContractId == id);
        return contract == null ? NotFound() : View(contract);
    }

    // Tenant has no generic financial write actions. Signing needs a dedicated workflow.
}
