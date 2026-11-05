using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Areas.Landlord.Controllers;

[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public class DashboardController : Controller
{
    public IActionResult Index()
    {
        return RedirectToAction("Index", "Overview", new { area = "Landlord" });
    }
}
