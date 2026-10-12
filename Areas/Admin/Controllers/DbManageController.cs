using EZ_Rent.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DbManageController : Controller
{
    public string StatusMessage { get; set; } = "";
    private readonly AppDbContext _dbContext;

    public DbManageController(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    public IActionResult DeleteDb()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDbAsync()
    {
        var success = await _dbContext.Database.EnsureDeletedAsync();

        StatusMessage = success
            ? "Xóa Database thành công"
            : "Xóa Database thất bại";

        return RedirectToAction(nameof(Index));
    }
    [HttpPost]
    public async Task<IActionResult> Migrate()
    {
        await _dbContext.Database.MigrateAsync();

        StatusMessage = "Tạo Database thành công";

        return RedirectToAction(nameof(Index));
    }
}