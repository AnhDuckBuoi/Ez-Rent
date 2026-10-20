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
public class ContractController : TenantControllerBase
{
    private readonly RentalDocumentService documents;
    private readonly RentalPrivateFileStore files;
    private readonly IWebHostEnvironment environment;
    private readonly TenantContractDraftService drafts;
    public ContractController(AppDbContext context, RentalDocumentService documents, RentalPrivateFileStore files,
        IWebHostEnvironment environment, TenantContractDraftService drafts) : base(context)
    {
        this.documents = documents;
        this.files = files;
        this.environment = environment;
        this.drafts = drafts;
    }

    private IQueryable<RentalContract> MyContracts => Context.RentalContracts.AsNoTracking()
        .Where(c => c.TenantId == CurrentUserId)
        .Include(c => c.RentalPost).Include(c => c.Tenant).Include(c => c.Landlord);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ContractListInput input)
    {
        if (!ModelState.IsValid)
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(new ContractListModel(input, [], 0, 1));
        }
        var query = MyContracts;
        if (input.Status.HasValue) query = query.Where(c => c.Status == input.Status.Value);
        var total = await query.CountAsync();
        var pages = TenantRentalPresentation.Pages(total);
        input.Page = Math.Min(input.Page, pages);
        var items = await query.OrderByDescending(c => c.StartDate).ThenByDescending(c => c.RentalContractId)
            .Skip((input.Page - 1) * TenantRentalPresentation.PageSize).Take(TenantRentalPresentation.PageSize).ToListAsync();
        return View(new ContractListModel(input, items, total, pages));
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int? id, int documentPage = 1)
    {
        if (!ModelState.IsValid || documentPage < 1) return BadRequest();
        var contract = await MyContracts.FirstOrDefaultAsync(c => c.RentalContractId == id);
        if (contract == null) return NotFound();
        var query = Context.ContractDocuments.AsNoTracking().Where(d => d.RentalContractId == contract.RentalContractId && d.TenantId == CurrentUserId);
        var pages = TenantRentalPresentation.Pages(await query.CountAsync());
        documentPage = Math.Min(documentPage, pages);
        var history = await query.OrderByDescending(d => d.Version).Skip((documentPage - 1) * TenantRentalPresentation.PageSize)
            .Take(TenantRentalPresentation.PageSize).ToListAsync();
        var current = await query.SingleOrDefaultAsync(d => d.Version == contract.CurrentDocumentVersion);
        return View(new ContractDetailsModel(contract, current, history, documentPage, pages));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmDocument(int id, int documentId, bool agreed, CancellationToken ct)
    {
        if (!ModelState.IsValid || id < 1 || documentId < 1 || !agreed) return BadRequest();
        var result = await documents.AcknowledgeAsync(id, documentId, CurrentUserId, ct);
        if (result == DocumentAcknowledgement.Missing) return NotFound();
        if (result == DocumentAcknowledgement.StaleVersion)
        {
            Response.StatusCode = StatusCodes.Status409Conflict;
            ViewData["RecoveryUrl"] = Url.Action(nameof(Details), new { id });
            ViewData["ConflictMessage"] = "Đã có bản hợp đồng mới. Hãy mở lại tài liệu và đối chiếu trước khi xác nhận.";
            return View("DocumentConflict");
        }
        TempData["DiscoveryMessage"] = "Đã ghi nhận bạn đối chiếu bản hợp đồng này.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Document(int id, bool download = false)
    {
        var document = await Context.ContractDocuments.AsNoTracking()
            .SingleOrDefaultAsync(d => d.ContractDocumentId == id && d.RentalContract.TenantId == CurrentUserId && d.TenantId == CurrentUserId);
        if (document == null || document.ContentType is not ("application/pdf" or "image/png" or "image/jpeg")) return NotFound();
        var stream = files.OpenRead(document.StorageKey);
        if (stream == null) return NotFound();
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'self'; sandbox";
        // PDFs download rather than execute inside the application's document origin.
        return download || document.ContentType == "application/pdf"
            ? File(stream, document.ContentType, document.FileName)
            : File(stream, document.ContentType);
    }

    // No Tenant endpoint changes contract terms or uploads Landlord documents.
    [HttpGet]
    public async Task<IActionResult> Template(int id)
    {
        if (!await MyContracts.AnyAsync(c => c.RentalContractId == id)) return NotFound();
        var path = Path.Combine(environment.ContentRootPath, "docs", "templates", "bm03-hop-dong-thue-phong.pdf");
        return System.IO.File.Exists(path) ? PhysicalFile(path, "application/pdf", "BM03-hop-dong-thue-phong-chua-dien.pdf") : NotFound();
    }

    [HttpGet]
    public async Task<IActionResult> Draft(int id, int? revision = null, int historyPage = 1, CancellationToken ct = default)
    {
        if (!ModelState.IsValid || id < 1 || revision < 1 || historyPage < 1) return BadRequest();
        var page = await drafts.GetAsync(id, CurrentUserId, revision, historyPage, ct: ct);
        return page == null ? NotFound() : View(page);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDraft(int id, [Bind(Prefix = "Input")] ContractTenantDraftInput input, CancellationToken ct)
    {
        if (ModelState.IsValid)
        {
            var result = await drafts.SaveAsync(id, CurrentUserId, input, ct);
            if (result.Status == DraftSaveStatus.Missing) return NotFound();
            if (result.Status == DraftSaveStatus.Saved)
            {
                TempData["DiscoveryMessage"] = $"Đã lưu bản nháp phiên bản {result.Revision}.";
                return RedirectToAction(nameof(Draft), new { id, revision = result.Revision });
            }
            if (result.Errors != null)
                foreach (var error in result.Errors) ModelState.AddModelError("Input." + error.Key, error.Value);
            var page = await drafts.GetAsync(id, CurrentUserId, attempted: input, ct: ct);
            if (page == null) return NotFound();
            if (result.Status == DraftSaveStatus.Conflict)
            {
                Response.StatusCode = StatusCodes.Status409Conflict;
                return View(nameof(Draft), page with { Conflict = true });
            }
            Response.StatusCode = StatusCodes.Status400BadRequest;
            return View(nameof(Draft), page);
        }
        var invalidPage = await drafts.GetAsync(id, CurrentUserId, attempted: input, ct: ct);
        if (invalidPage == null) return NotFound();
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(nameof(Draft), invalidPage);
    }
}
