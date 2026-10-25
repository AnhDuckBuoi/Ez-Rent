using System.Security.Claims;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using EZ_Rent.Areas.Tenant.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace EZ_Rent.Areas.Tenant.Controllers;

public abstract class TenantControllerBase : Controller
{
    protected AppDbContext Context { get; }
    protected string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    protected TenantControllerBase(AppDbContext context) => Context = context;

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        // Fail closed if an authenticated principal has no Identity user ID.
        if (string.IsNullOrWhiteSpace(CurrentUserId)) context.Result = Forbid();
        base.OnActionExecuting(context);
    }

    protected IQueryable<RentalPost> PublicAvailablePosts => Context.RentalPosts.VisibleToTenant();

    protected IQueryable<RentalPost> MyRentedPosts => Context.RentalPosts
        .Where(p => p.RentalContracts.Any(c => c.TenantId == CurrentUserId && c.Status == ContractStatus.Active));
}
