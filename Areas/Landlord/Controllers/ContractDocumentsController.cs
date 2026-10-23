using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;
using EZ_Rent.Services.Common;
using EZ_Rent.Services.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// Tài liệu hợp đồng (BM03 đã thống nhất/đã ký, dạng PDF hoặc ảnh) theo phiên bản – phần "Đức cần ghép phía Landlord"
/// trong tài liệu của Tenant (docs/tenant-documents-mvc.md):
/// - Gọi RentalDocumentService.AppendContractAsync(contractId, chủ trọ đang đăng nhập, expectedVersion, file);
///   phiên bản so bằng cập nhật có điều kiện: hai tab cùng gửi thì tab sau báo xung đột, không ghi đè.
/// - Người nộp lấy từ phiên đăng nhập, không nhận từ form; giới hạn multipart; file nằm ngoài wwwroot.
/// - Tải về luôn dạng đính kèm (không nhúng PDF vào origin ứng dụng), chỉ chủ trọ của hợp đồng.
/// Người thuê xem và bấm "Đã đối chiếu" ở trang hợp đồng phía Tenant; đó là điều kiện để chủ trọ kích hoạt hợp đồng.
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class ContractDocumentsController(AppDbContext db, RentalDocumentService documents, RentalPrivateFileStore files,
    TimeProvider? clock = null) : LandlordAreaController(db, clock)
{
    private string ContractPage(int id) => (Url.Action("Details", "Contracts", new { id }) ?? "/Landlord/Contracts") + "#documents";

    [HttpPost]
    [RequestSizeLimit(RentalPrivateFileStore.DocumentMaxBytes + 64 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = RentalPrivateFileStore.DocumentMaxBytes + 64 * 1024)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromRoute] int id, [FromForm] int expectedVersion, IFormFile? file)
    {
        var back = ContractPage(id);
        var contract = await MyContracts.AsNoTracking().Where(c => c.RentalContractId == id)
            .Select(c => new { c.TenantId, c.Status, c.RentalPost.Title }).FirstOrDefaultAsync();
        if (contract == null) return NotFoundPage("hợp đồng", Url.Action("Index", "Contracts") ?? "/Landlord/Contracts");
        if (contract.Status is ContractStatus.Cancelled or ContractStatus.Expired)
            return Fail(StatusCodes.Status409Conflict, "Hợp đồng đã đóng nên không thêm tài liệu mới.", back);
        if (file == null || file.Length == 0) return Fail(StatusCodes.Status400BadRequest, "Chọn file PDF, PNG hoặc JPG của hợp đồng.", back);
        try
        {
            var document = await documents.AppendContractAsync(id, CurrentUserId, expectedVersion, file, HttpContext.RequestAborted);
            TenantNotifier.Add(Db, contract.TenantId, "Chủ nhà gửi tài liệu hợp đồng mới",
                $"{contract.Title}: phiên bản {document.Version}. Hãy xem kỹ rồi bấm đã đối chiếu trên trang hợp đồng.",
                TenantNotifier.ContractLink(id), "contract", UtcNow);
            await Db.SaveChangesAsync();
            return Done($"Đã lưu tài liệu phiên bản {document.Version}. Người thuê cần đối chiếu bản này trước khi bạn xác nhận hiệu lực.", back);
        }
        catch (RentalDocumentConflictException ex) { return Fail(StatusCodes.Status409Conflict, ex.Message, back); }
        catch (InvalidDataException ex) { return Fail(StatusCodes.Status400BadRequest, ex.Message, back); }
        catch (KeyNotFoundException) { return NotFoundPage("hợp đồng", Url.Action("Index", "Contracts") ?? "/Landlord/Contracts"); }
    }

    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Download([FromRoute] int id, [FromQuery] int documentId)
    {
        var document = await Db.ContractDocuments.AsNoTracking()
            .Where(d => d.ContractDocumentId == documentId && d.RentalContractId == id && d.RentalContract.LandlordId == CurrentUserId)
            .Select(d => new { d.StorageKey, d.FileName, d.ContentType }).FirstOrDefaultAsync();
        if (document == null) return NotFound();
        var stream = files.OpenRead(document.StorageKey);
        if (stream == null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, document.ContentType, string.IsNullOrWhiteSpace(document.FileName) ? "hop-dong" : document.FileName);
    }
}
