using EZ_Rent.Data;
using EZ_Rent.Services;
using EZ_Rent.Services.Meters;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// Trả ảnh công tơ từ kho riêng tư (ngoài wwwroot) cho đúng chủ trọ sở hữu phòng:
/// - Reading: ảnh chủ trọ chụp khi ghi chỉ số (UC16/BM04);
/// - Evidence: bản ảnh đã lưu đối chiếu cùng hóa đơn (InvoiceMeterEvidence, người thuê xem cùng bộ này).
/// Không có đường dẫn công khai nào tới ảnh; id của người khác trả 404.
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class MeterPhotosController(AppDbContext db, RentalPrivateFileStore files, TimeProvider? clock = null)
    : LandlordAreaController(db, clock)
{
    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reading([FromRoute] int id, [FromQuery] string side)
    {
        var reading = await MyReadings.AsNoTracking().Where(m => m.MeterReadingId == id)
            .Select(m => new { m.ElectricMeterImageUrl, m.WaterMeterImageUrl }).FirstOrDefaultAsync();
        if (reading == null) return NotFound();
        return Serve(MeterPhotoFiles.KeyOf(side == "water" ? reading.WaterMeterImageUrl : reading.ElectricMeterImageUrl));
    }

    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Evidence([FromRoute] int id, [FromQuery] string side)
    {
        var evidence = await Db.InvoiceMeterEvidence.AsNoTracking()
            .Where(e => e.InvoiceId == id && e.Invoice.RentalContract.LandlordId == CurrentUserId)
            .Select(e => new { e.ElectricStorageKey, e.WaterStorageKey }).FirstOrDefaultAsync();
        if (evidence == null) return NotFound();
        return Serve(side == "water" ? evidence.WaterStorageKey : evidence.ElectricStorageKey);
    }

    private IActionResult Serve(string? key)
    {
        if (string.IsNullOrEmpty(key)) return NotFound();
        var stream = files.OpenRead(key);
        if (stream == null) return NotFound();
        Response.Headers.CacheControl = "private, no-store";
        Response.Headers.XContentTypeOptions = "nosniff";
        return File(stream, MeterPhotoFiles.ContentTypeOf(key));
    }
}
