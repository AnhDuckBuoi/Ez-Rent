using EZ_Rent.Areas.Landlord.Services;
using EZ_Rent.Areas.Landlord.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC16 Ghi nhận chỉ số điện nước, UC17 Xem chỉ số (P06, BM04).
/// - Chỉ số kỳ trước lấy từ bản ghi đã lưu (người dùng không sửa được), trừ lần ghi đầu tiên của phòng.
/// - Lượng tiêu thụ và tiền điện/nước tính ở server; trang gọi AJAX /Preview để xem trước khi lưu.
/// - Ảnh công tơ lưu kèm bản ghi để người thuê đối chiếu qua hóa đơn.
/// - Bản ghi đã dùng để lập hóa đơn không sửa/xóa được.
/// </summary>
public sealed class MeterReadingsController(AppDbContext db, IWebHostEnvironment env, TimeProvider? clock = null)
    : LandlordControllerBase(db, clock)
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] MeterIndexInput input)
    {
        if (!ModelState.IsValid) return InvalidQuery(Back(nameof(Index)));
        var current = LandlordTime.Period(VietnamNow);
        var period = input.Period ?? current;
        if (!LandlordTime.TryParsePeriod(period, out _) || string.CompareOrdinal(period, current) > 0)
            return InvalidQuery(Back(nameof(Index)));

        var rooms = MyPosts.AsNoTracking().Where(p => p.RentalContracts.Any(c => c.Status == ContractStatus.Active));
        var total = await rooms.CountAsync();
        var recorded = await rooms.CountAsync(p => p.MeterReadings.Any(m => m.Period == period));
        var page = PagedList<MeterRoomRow>.ClampPage(input.Page, total, PageSize);
        var rows = await rooms
            .OrderBy(p => p.MeterReadings.Any(m => m.Period == period) ? 1 : 0).ThenBy(p => p.Title).ThenBy(p => p.RentalPostId)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(p => new MeterRoomRow
            {
                RoomId = p.RentalPostId, RoomTitle = p.Title,
                TenantName = p.RentalContracts.Where(c => c.Status == ContractStatus.Active).Select(c => c.Tenant.FullName).FirstOrDefault() ?? "",
                ContractId = p.RentalContracts.Where(c => c.Status == ContractStatus.Active).Select(c => (int?)c.RentalContractId).FirstOrDefault(),
                ReadingId = p.MeterReadings.Where(m => m.Period == period).Select(m => (int?)m.MeterReadingId).FirstOrDefault(),
                ElectricUsage = p.MeterReadings.Where(m => m.Period == period).Select(m => (double?)m.ElectricUsage).FirstOrDefault(),
                WaterUsage = p.MeterReadings.Where(m => m.Period == period).Select(m => (double?)m.WaterUsage).FirstOrDefault(),
                LastPeriod = p.MeterReadings.OrderByDescending(m => m.Period).Select(m => m.Period).FirstOrDefault(),
                HasInvoice = p.MeterReadings.Where(m => m.Period == period)
                    .Any(m => m.Invoices.Any(i => i.PaymentStatus != PaymentStatus.Cancelled))
            }).ToListAsync();
        input.Page = page;
        return PageOrRegion("Index", "_MeterRooms", new MeterIndexModel
        {
            Period = period, CurrentPeriod = current, Recorded = recorded,
            Rooms = new PagedList<MeterRoomRow> { Items = rows, Page = page, PageSize = PageSize, Total = total }
        });
    }

    [HttpGet]
    public async Task<IActionResult> History([FromRoute] int id, [FromQuery] int page = 1)
    {
        var room = await MyPosts.AsNoTracking().Where(p => p.RentalPostId == id).Select(p => new { p.Title }).FirstOrDefaultAsync();
        if (room == null) return MissingPage("phòng", Back(nameof(Index)));
        if (page < 1) return InvalidQuery(Back(nameof(History), new { id }));
        var query = Db.MeterReadings.AsNoTracking().Where(m => m.RentalPostId == id);
        var total = await query.CountAsync();
        page = PagedList<MeterHistoryRow>.ClampPage(page, total, PageSize);
        var rows = await query.OrderByDescending(m => m.Period).Skip((page - 1) * PageSize).Take(PageSize)
            .Select(m => new MeterHistoryRow
            {
                Id = m.MeterReadingId, Period = m.Period, PrevElectric = m.PrevElectricIndex, CurrElectric = m.CurrElectricIndex,
                ElectricUsage = m.ElectricUsage, PrevWater = m.PrevWaterIndex, CurrWater = m.CurrWaterIndex, WaterUsage = m.WaterUsage,
                ReadingDate = m.ReadingDate, HasPhotos = m.ElectricMeterImageUrl != null || m.WaterMeterImageUrl != null,
                InvoiceId = m.Invoices.Where(i => i.PaymentStatus != PaymentStatus.Cancelled).Select(i => (int?)i.InvoiceId).FirstOrDefault()
            }).ToListAsync();
        return PageOrRegion("History", "_HistoryList", new MeterHistoryModel
        {
            RoomId = id, RoomTitle = room.Title,
            Items = new PagedList<MeterHistoryRow> { Items = rows, Page = page, PageSize = PageSize, Total = total }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create([FromQuery] int roomId, [FromQuery] string? period)
    {
        period ??= LandlordTime.Period(VietnamNow);
        var problem = await CheckRoomPeriodAsync(roomId, period);
        if (problem != null) return Fail(StatusCodes.Status400BadRequest, problem, Back(nameof(Index)));
        var existing = await Db.MeterReadings.Where(m => m.RentalPostId == roomId && m.Period == period).Select(m => (int?)m.MeterReadingId).FirstOrDefaultAsync();
        if (existing.HasValue)
        {
            TempData[LandlordFlash.Key] = "Phòng này đã ghi chỉ số kỳ " + LandlordTime.PeriodLabel(period) + ".";
            return RedirectToAction(nameof(Details), new { id = existing.Value });
        }
        return View("Create", await FormAsync(new MeterInput { RentalPostId = roomId, Period = period }, null));
    }

    [HttpPost]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] MeterInput input)
    {
        var problem = await CheckRoomPeriodAsync(input.RentalPostId, input.Period);
        if (problem != null) ModelState.AddModelError("Input.Period", problem);
        if (await Db.MeterReadings.AnyAsync(m => m.RentalPostId == input.RentalPostId && m.Period == input.Period))
            ModelState.AddModelError("Input.Period", "Kỳ này đã có chỉ số.");
        var previous = await PreviousAsync(input.RentalPostId, input.Period);
        var values = ReadValues(input, previous);
        var (electricUpload, waterUpload) = await CheckPhotosAsync(input);
        if (!ModelState.IsValid || values == null) return View("Create", await FormAsync(input, null));

        var reading = new MeterReading
        {
            RentalPostId = input.RentalPostId, Period = input.Period, ReadingDate = UtcNow, RecordedById = CurrentUserId,
            PrevElectricIndex = values.Value.PrevE, CurrElectricIndex = values.Value.CurrE, ElectricUsage = LandlordBilling.Usage(values.Value.PrevE, values.Value.CurrE),
            PrevWaterIndex = values.Value.PrevW, CurrWaterIndex = values.Value.CurrW, WaterUsage = LandlordBilling.Usage(values.Value.PrevW, values.Value.CurrW),
            Note = LandlordFormat.Clean(input.Note, 500)
        };
        if (electricUpload != null) reading.ElectricMeterImageUrl = await LandlordUploads.SaveAsync(env, electricUpload, "meters", input.RentalPostId, HttpContext.RequestAborted);
        if (waterUpload != null) reading.WaterMeterImageUrl = await LandlordUploads.SaveAsync(env, waterUpload, "meters", input.RentalPostId, HttpContext.RequestAborted);
        Db.MeterReadings.Add(reading);
        try { await Db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            LandlordUploads.TryDelete(env, reading.ElectricMeterImageUrl);
            LandlordUploads.TryDelete(env, reading.WaterMeterImageUrl);
            ModelState.AddModelError("Input.Period", "Kỳ này vừa được ghi chỉ số ở một cửa sổ khác.");
            return View("Create", await FormAsync(input, null));
        }
        return Done($"Đã lưu chỉ số {LandlordTime.PeriodLabel(input.Period)}. Bạn có thể lập hóa đơn ngay.",
            Back(nameof(Details), new { id = reading.MeterReadingId }));
    }

    /// <summary>
    /// AJAX "tính tiền": nhận chỉ số đang nhập, trả lượng tiêu thụ và số tiền theo giá trong hợp đồng hiệu lực.
    /// Server là nơi tính duy nhất, giao diện chỉ hiển thị kết quả.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Preview([Bind(Prefix = "Input")] MeterInput input, [FromQuery] int? id)
    {
        ModelState.Clear();
        var room = await MyPosts.AsNoTracking().Where(p => p.RentalPostId == input.RentalPostId)
            .Select(p => new
            {
                Contract = p.RentalContracts.Where(c => c.Status == ContractStatus.Active)
                    .Select(c => new { c.AgreedElectricityPrice, c.AgreedWaterPrice }).FirstOrDefault()
            }).FirstOrDefaultAsync();
        if (room == null) { Response.StatusCode = 404; return Json(new { ok = false, message = "Không tìm thấy phòng." }); }
        MeterReading? previous;
        if (id.HasValue)
        {
            var self = await MyReadings.AsNoTracking().FirstOrDefaultAsync(m => m.MeterReadingId == id.Value);
            if (self == null) { Response.StatusCode = 404; return Json(new { ok = false, message = "Không tìm thấy bản ghi." }); }
            previous = await PreviousAsync(self.RentalPostId, self.Period);
            input.Period = self.Period;
        }
        else previous = await PreviousAsync(input.RentalPostId, input.Period ?? "");
        var errors = new List<string>();
        var values = ReadValues(input, previous, errors);
        var ePrice = room.Contract?.AgreedElectricityPrice ?? 0;
        var wPrice = room.Contract?.AgreedWaterPrice ?? 0;
        if (values == null) return Json(new { ok = false, message = string.Join(" ", errors) });
        var eUsage = LandlordBilling.Usage(values.Value.PrevE, values.Value.CurrE);
        var wUsage = LandlordBilling.Usage(values.Value.PrevW, values.Value.CurrW);
        var eAmount = LandlordBilling.Amount(eUsage, ePrice);
        var wAmount = LandlordBilling.Amount(wUsage, wPrice);
        var warnings = new List<string>();
        if (previous != null && previous.ElectricUsage > 0 && eUsage > previous.ElectricUsage * 2.5)
            warnings.Add($"Điện dùng nhiều hơn 2,5 lần kỳ trước ({LandlordFormat.Number(previous.ElectricUsage)} kWh). Kiểm tra lại số trên công tơ.");
        if (previous != null && previous.WaterUsage > 0 && wUsage > previous.WaterUsage * 2.5)
            warnings.Add($"Nước dùng nhiều hơn 2,5 lần kỳ trước ({LandlordFormat.Number(previous.WaterUsage)} m³).");
        return Json(new
        {
            ok = true,
            electricUsage = LandlordFormat.Number(eUsage) + " kWh", waterUsage = LandlordFormat.Number(wUsage) + " m³",
            electricAmount = LandlordFormat.Money(eAmount), waterAmount = LandlordFormat.Money(wAmount),
            total = LandlordFormat.Money(eAmount + wAmount),
            electricFormula = $"{LandlordFormat.Number(eUsage)} × {LandlordFormat.Number(ePrice)}",
            waterFormula = $"{LandlordFormat.Number(wUsage)} × {LandlordFormat.Number(wPrice)}",
            message = string.Join(" ", warnings)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var m = await MyReadings.AsNoTracking().Where(x => x.MeterReadingId == id).Select(x => new
        {
            x, x.RentalPost.Title, Recorder = x.RecordedBy.FullName,
            InvoiceId = x.Invoices.Where(i => i.PaymentStatus != PaymentStatus.Cancelled).Select(i => (int?)i.InvoiceId).FirstOrDefault(),
            Latest = !Db.MeterReadings.Any(o => o.RentalPostId == x.RentalPostId && string.Compare(o.Period, x.Period) > 0),
            Contract = x.RentalPost.RentalContracts.Where(c => c.Status == ContractStatus.Active)
                .Select(c => new { c.AgreedElectricityPrice, c.AgreedWaterPrice }).FirstOrDefault()
        }).FirstOrDefaultAsync();
        if (m == null) return MissingPage("bản ghi chỉ số", Back(nameof(Index)));
        return View("Details", new MeterDetailModel
        {
            Id = id, RoomId = m.x.RentalPostId, RoomTitle = m.Title, Period = m.x.Period, ReadingDate = m.x.ReadingDate,
            PrevElectric = m.x.PrevElectricIndex, CurrElectric = m.x.CurrElectricIndex, ElectricUsage = m.x.ElectricUsage,
            PrevWater = m.x.PrevWaterIndex, CurrWater = m.x.CurrWaterIndex, WaterUsage = m.x.WaterUsage,
            ElectricPhotoUrl = LandlordFormat.SafeMediaUrl(m.x.ElectricMeterImageUrl), WaterPhotoUrl = LandlordFormat.SafeMediaUrl(m.x.WaterMeterImageUrl),
            Note = m.x.Note, RecordedBy = m.Recorder, InvoiceId = m.InvoiceId, IsLatest = m.Latest,
            ElectricPrice = m.Contract?.AgreedElectricityPrice, WaterPrice = m.Contract?.AgreedWaterPrice
        });
    }

    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] int id)
    {
        var m = await MyReadings.AsNoTracking().FirstOrDefaultAsync(x => x.MeterReadingId == id);
        if (m == null) return MissingPage("bản ghi chỉ số", Back(nameof(Index)));
        if (await InvoicedAsync(id))
            return Fail(StatusCodes.Status409Conflict, "Chỉ số đã dùng để lập hóa đơn. Hủy hóa đơn trước nếu cần ghi lại.", Back(nameof(Details), new { id }));
        var input = new MeterInput
        {
            RentalPostId = m.RentalPostId, Period = m.Period,
            PrevElectricIndex = MeterInput.Show(m.PrevElectricIndex), CurrElectricIndex = MeterInput.Show(m.CurrElectricIndex),
            PrevWaterIndex = MeterInput.Show(m.PrevWaterIndex), CurrWaterIndex = MeterInput.Show(m.CurrWaterIndex), Note = m.Note
        };
        return View("Edit", await FormAsync(input, m));
    }

    [HttpPost]
    [RequestSizeLimit(12 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 12 * 1024 * 1024)]
    public async Task<IActionResult> Edit([FromRoute] int id, [Bind(Prefix = "Input")] MeterInput input)
    {
        var m = await MyReadings.FirstOrDefaultAsync(x => x.MeterReadingId == id);
        if (m == null) return MissingPage("bản ghi chỉ số", Back(nameof(Index)));
        if (await InvoicedAsync(id))
            return Fail(StatusCodes.Status409Conflict, "Chỉ số đã dùng để lập hóa đơn nên không sửa được.", Back(nameof(Details), new { id }));
        // Phòng và kỳ của bản ghi cố định; chỉ sửa chỉ số, ghi chú và ảnh.
        input.RentalPostId = m.RentalPostId; input.Period = m.Period;
        ModelState.Remove("Input.Period"); ModelState.Remove("Input.RentalPostId");
        var previous = await PreviousAsync(m.RentalPostId, m.Period);
        var values = ReadValues(input, previous);
        var (electricUpload, waterUpload) = await CheckPhotosAsync(input);
        if (!ModelState.IsValid || values == null) return View("Edit", await FormAsync(input, m));
        // Không để chỉ số kỳ này nhỏ hơn chỉ số "kỳ trước" của kỳ sau (nếu đã có kỳ sau).
        var next = await Db.MeterReadings.AsNoTracking().Where(x => x.RentalPostId == m.RentalPostId && string.Compare(x.Period, m.Period) > 0)
            .OrderBy(x => x.Period).FirstOrDefaultAsync();
        if (next != null && (values.Value.CurrE > next.CurrElectricIndex || values.Value.CurrW > next.CurrWaterIndex))
        {
            ModelState.AddModelError("Input.CurrElectricIndex", $"Chỉ số không được lớn hơn chỉ số đã ghi ở {LandlordTime.PeriodLabel(next.Period)}.");
            return View("Edit", await FormAsync(input, m));
        }
        var nextInvoiced = next != null && await InvoicedAsync(next.MeterReadingId);
        if (nextInvoiced && (values.Value.CurrE != m.CurrElectricIndex || values.Value.CurrW != m.CurrWaterIndex))
        {
            ModelState.AddModelError("Input.CurrElectricIndex", $"{LandlordTime.PeriodLabel(next!.Period)} đã lập hóa đơn theo chỉ số này nên không đổi được chỉ số hiện tại.");
            return View("Edit", await FormAsync(input, m));
        }
        m.PrevElectricIndex = values.Value.PrevE; m.CurrElectricIndex = values.Value.CurrE; m.ElectricUsage = LandlordBilling.Usage(values.Value.PrevE, values.Value.CurrE);
        m.PrevWaterIndex = values.Value.PrevW; m.CurrWaterIndex = values.Value.CurrW; m.WaterUsage = LandlordBilling.Usage(values.Value.PrevW, values.Value.CurrW);
        m.Note = LandlordFormat.Clean(input.Note, 500);
        var oldFiles = new List<string?>();
        if (electricUpload != null) { oldFiles.Add(m.ElectricMeterImageUrl); m.ElectricMeterImageUrl = await LandlordUploads.SaveAsync(env, electricUpload, "meters", m.RentalPostId, HttpContext.RequestAborted); }
        if (waterUpload != null) { oldFiles.Add(m.WaterMeterImageUrl); m.WaterMeterImageUrl = await LandlordUploads.SaveAsync(env, waterUpload, "meters", m.RentalPostId, HttpContext.RequestAborted); }
        // Kỳ sau dùng chỉ số kỳ này làm "kỳ trước": cập nhật theo để chuỗi số liệu liền mạch.
        if (next != null && !nextInvoiced)
        {
            var tracked = await Db.MeterReadings.FirstAsync(x => x.MeterReadingId == next.MeterReadingId);
            tracked.PrevElectricIndex = m.CurrElectricIndex; tracked.ElectricUsage = LandlordBilling.Usage(m.CurrElectricIndex, tracked.CurrElectricIndex);
            tracked.PrevWaterIndex = m.CurrWaterIndex; tracked.WaterUsage = LandlordBilling.Usage(m.CurrWaterIndex, tracked.CurrWaterIndex);
        }
        await Db.SaveChangesAsync();
        foreach (var url in oldFiles) LandlordUploads.TryDelete(env, url);
        return Done("Đã cập nhật chỉ số.", Back(nameof(Details), new { id }));
    }

    [HttpPost]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        var m = await MyReadings.FirstOrDefaultAsync(x => x.MeterReadingId == id);
        if (m == null) return MissingPage("bản ghi chỉ số", Back(nameof(Index)));
        var back = Back(nameof(Details), new { id });
        if (await InvoicedAsync(id)) return Fail(StatusCodes.Status409Conflict, "Chỉ số đã dùng để lập hóa đơn nên không xóa được.", back);
        if (await Db.MeterReadings.AnyAsync(x => x.RentalPostId == m.RentalPostId && string.Compare(x.Period, m.Period) > 0))
            return Fail(StatusCodes.Status409Conflict, "Chỉ xóa được kỳ ghi gần nhất của phòng để không làm đứt chuỗi chỉ số.", back);
        var files = new[] { m.ElectricMeterImageUrl, m.WaterMeterImageUrl };
        Db.MeterReadings.Remove(m);
        await Db.SaveChangesAsync();
        foreach (var url in files) LandlordUploads.TryDelete(env, url);
        return Done("Đã xóa bản ghi chỉ số.", Back(nameof(Index), new { period = m.Period }));
    }

    // ===================== Hỗ trợ =====================

    private async Task<string?> CheckRoomPeriodAsync(int roomId, string? period)
    {
        if (!LandlordTime.TryParsePeriod(period, out var first)) return "Kỳ ghi nhận không hợp lệ.";
        if (string.CompareOrdinal(period, LandlordTime.Period(VietnamNow)) > 0) return "Không ghi chỉ số cho tháng chưa tới.";
        var contract = await MyPosts.AsNoTracking().Where(p => p.RentalPostId == roomId)
            .Select(p => p.RentalContracts.Where(c => c.Status == ContractStatus.Active).Select(c => new { c.StartDate }).FirstOrDefault())
            .FirstOrDefaultAsync();
        if (contract == null) return "Chỉ ghi chỉ số cho phòng của bạn đang có hợp đồng hiệu lực.";
        if (first.AddMonths(1) <= contract.StartDate.Date) return "Kỳ này trước ngày bắt đầu hợp đồng.";
        return null;
    }

    private Task<MeterReading?> PreviousAsync(int roomId, string period) =>
        Db.MeterReadings.AsNoTracking().Where(m => m.RentalPostId == roomId && string.Compare(m.Period, period) < 0)
            .OrderByDescending(m => m.Period).FirstOrDefaultAsync();

    private Task<bool> InvoicedAsync(int readingId) =>
        Db.Invoices.AnyAsync(i => i.MeterReadingId == readingId && i.PaymentStatus != PaymentStatus.Cancelled);

    private (double PrevE, double CurrE, double PrevW, double CurrW)? ReadValues(MeterInput input, MeterReading? previous, List<string>? errors = null)
    {
        void Error(string key, string message) { if (errors != null) errors.Add(message); else ModelState.AddModelError(key, message); }
        double prevE, prevW;
        if (previous != null) { prevE = previous.CurrElectricIndex; prevW = previous.CurrWaterIndex; }
        else
        {
            if (!MeterInput.TryRead(input.PrevElectricIndex ?? "0", out prevE)) Error("Input.PrevElectricIndex", "Chỉ số điện đầu kỳ không hợp lệ.");
            if (!MeterInput.TryRead(input.PrevWaterIndex ?? "0", out prevW)) Error("Input.PrevWaterIndex", "Chỉ số nước đầu kỳ không hợp lệ.");
        }
        var okE = MeterInput.TryRead(input.CurrElectricIndex, out var currE);
        var okW = MeterInput.TryRead(input.CurrWaterIndex, out var currW);
        if (!okE) Error("Input.CurrElectricIndex", "Nhập chỉ số điện hiện tại.");
        if (!okW) Error("Input.CurrWaterIndex", "Nhập chỉ số nước hiện tại.");
        if (okE && currE < prevE) Error("Input.CurrElectricIndex", $"Chỉ số điện phải ≥ chỉ số kỳ trước ({MeterInput.Show(prevE)}).");
        if (okW && currW < prevW) Error("Input.CurrWaterIndex", $"Chỉ số nước phải ≥ chỉ số kỳ trước ({MeterInput.Show(prevW)}).");
        if (okE && currE - prevE > LandlordBilling.MaxUsage) Error("Input.CurrElectricIndex", "Lượng điện tiêu thụ quá lớn, kiểm tra lại.");
        if (okW && currW - prevW > LandlordBilling.MaxUsage) Error("Input.CurrWaterIndex", "Lượng nước tiêu thụ quá lớn, kiểm tra lại.");
        var hasError = errors != null ? errors.Count > 0 : !ModelState.IsValid;
        return hasError || !okE || !okW ? null : (prevE, currE, prevW, currW);
    }

    private async Task<(CheckedUpload? Electric, CheckedUpload? Water)> CheckPhotosAsync(MeterInput input)
    {
        CheckedUpload? e = null, w = null;
        if (input.ElectricPhoto is { Length: > 0 })
        {
            var (u, error) = await LandlordUploads.CheckAsync(input.ElectricPhoto, allowVideo: false, HttpContext.RequestAborted);
            if (error != null) ModelState.AddModelError("Input.ElectricPhoto", error); else e = u;
        }
        if (input.WaterPhoto is { Length: > 0 })
        {
            var (u, error) = await LandlordUploads.CheckAsync(input.WaterPhoto, allowVideo: false, HttpContext.RequestAborted);
            if (error != null) ModelState.AddModelError("Input.WaterPhoto", error); else w = u;
        }
        return (e, w);
    }

    private async Task<MeterFormModel> FormAsync(MeterInput input, MeterReading? existing)
    {
        var room = await MyPosts.AsNoTracking().Where(p => p.RentalPostId == input.RentalPostId).Select(p => new
        {
            p.Title,
            Contract = p.RentalContracts.Where(c => c.Status == ContractStatus.Active)
                .Select(c => new { c.Tenant.FullName, c.AgreedElectricityPrice, c.AgreedWaterPrice }).FirstOrDefault()
        }).FirstOrDefaultAsync();
        var previous = await PreviousAsync(input.RentalPostId, input.Period);
        if (previous != null)
        {
            input.PrevElectricIndex = MeterInput.Show(previous.CurrElectricIndex);
            input.PrevWaterIndex = MeterInput.Show(previous.CurrWaterIndex);
        }
        return new MeterFormModel
        {
            Id = existing?.MeterReadingId, Input = input, RoomTitle = room?.Title ?? "", TenantName = room?.Contract?.FullName ?? "",
            ElectricPrice = room?.Contract?.AgreedElectricityPrice ?? 0, WaterPrice = room?.Contract?.AgreedWaterPrice ?? 0,
            HasPrevious = previous != null, PreviousPeriod = previous?.Period,
            ElectricPhotoUrl = LandlordFormat.SafeMediaUrl(existing?.ElectricMeterImageUrl),
            WaterPhotoUrl = LandlordFormat.SafeMediaUrl(existing?.WaterMeterImageUrl)
        };
    }
}
