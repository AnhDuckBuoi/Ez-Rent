using EZ_Rent.Areas.Landlord.Helpers;
using EZ_Rent.Services.Billing;
using EZ_Rent.Services.Common;
using EZ_Rent.Services.Notifications;
using EZ_Rent.Services.Payments;
using EZ_Rent.Services.Uploads;
using EZ_Rent.Services.Viewings;
using EZ_Rent.Areas.Landlord.ViewModels.Account;
using EZ_Rent.Areas.Landlord.ViewModels.Contracts;
using EZ_Rent.Areas.Landlord.ViewModels.Invoices;
using EZ_Rent.Areas.Landlord.ViewModels.Meters;
using EZ_Rent.Areas.Landlord.ViewModels.Overview;
using EZ_Rent.Areas.Landlord.ViewModels.Payments;
using EZ_Rent.Areas.Landlord.ViewModels.Repairs;
using EZ_Rent.Areas.Landlord.ViewModels.Rooms;
using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Areas.Landlord.ViewModels.Support;
using EZ_Rent.Areas.Landlord.ViewModels.Viewings;
using EZ_Rent.Data;
using Microsoft.Extensions.Logging;
using EZ_Rent.Services;
using EZ_Rent.Services.Meters;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC18 Lập hóa đơn, UC19 Xem hóa đơn (P07, BM05): danh sách, lập, xem trước bằng AJAX, sửa, xem chi tiết.
/// Xác nhận thanh toán, hủy, tạo lại mã QR (UC21) nằm ở InvoicePaymentsController.
/// - Tiền phòng, giá điện/nước lấy từ hợp đồng; lượng tiêu thụ lấy từ chỉ số cùng kỳ; server tính tổng.
/// - Phát hành kèm điện nước: lưu bộ đối chiếu (InvoiceMeterEvidence) qua RentalDocumentService của nhóm.
/// - VietQR: tạo chuỗi chuẩn NAPAS từ tài khoản nhận tiền của chủ trọ, lưu vào Invoice.VietQRContent.
/// - Thanh toán 2 bước theo báo cáo: người thuê báo đã chuyển (AwaitingConfirmation, phía Tenant) →
///   chủ trọ đối soát và "Xác nhận đã nhận tiền" → Paid. Chủ trọ cũng xác nhận được khi nhận tiền mặt.
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class InvoicesController(AppDbContext db, RentalDocumentService documents, RentalPrivateFileStore files, TimeProvider? clock = null)
    : LandlordAreaController(db, clock)
{
    private static readonly string[] Keys = ["open", "awaiting", "overdue", "paid", "cancelled"];

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] InvoiceListInput input)
    {
        if (!ModelState.IsValid) return InvalidQuery(Back(nameof(Index)));
        var today = VietnamToday;
        var mine = MyInvoices.AsNoTracking();
        if (!string.IsNullOrEmpty(input.Period)) mine = mine.Where(i => i.BillingPeriod == input.Period);
        var counts = new Dictionary<string, int>();
        foreach (var key in Keys) counts[key] = await Filter(mine, key, today).CountAsync();
        var openAmount = await Filter(mine, "open", today).SumAsync(i => (decimal?)i.TotalAmount) ?? 0;
        var query = string.IsNullOrEmpty(input.Status) ? mine : Filter(mine, input.Status, today);
        var items = await PageAsync(query
            .OrderBy(i => i.PaymentStatus == PaymentStatus.AwaitingConfirmation ? 0
                : i.PaymentStatus == PaymentStatus.Unpaid || i.PaymentStatus == PaymentStatus.Overdue ? 1 : 2)
            .ThenByDescending(i => i.BillingPeriod).ThenByDescending(i => i.InvoiceId)
            .Select(i => new InvoiceRow
            {
                Id = i.InvoiceId, Period = i.BillingPeriod, RoomTitle = i.RentalContract.RentalPost.Title,
                TenantName = i.RentalContract.Tenant.FullName, Total = i.TotalAmount, DueDate = i.DueDate,
                Status = i.PaymentStatus, TenantConfirmedAt = i.TenantConfirmedAt,
                Overdue = (i.PaymentStatus == PaymentStatus.Unpaid || i.PaymentStatus == PaymentStatus.Overdue) && i.DueDate < today
            }), input.Page);
        input.Page = items.PageIndex;
        return PageOrRegion("Index", "_InvoiceList", new InvoiceListModel
        {
            Input = input, Counts = counts, OpenAmount = openAmount, Items = items, Pager = Pager(items)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create([FromQuery] int? contractId, [FromQuery] string? period)
    {
        var input = new InvoiceInput
        {
            RentalContractId = contractId ?? 0,
            BillingPeriod = period ?? VietnamTime.Period(VietnamNow),
            DueDate = VietnamToday.AddDays(7)
        };
        return View("Create", await FormAsync(input, null, preview: true));
    }

    /// <summary>AJAX: tính lại bảng tiền khi đổi hợp đồng, kỳ, dịch vụ hay khoản điều chỉnh (không lưu gì).</summary>
    [HttpGet]
    public async Task<IActionResult> Calculate([Bind(Prefix = "Input")] InvoiceInput input, [FromQuery] int? id)
    {
        ModelState.Clear();
        var result = await ComputeAsync(input, id, collectErrors: true);
        if (result.Error != null) return Json(new { success = false, message = result.Error });
        var b = result.Breakdown!;
        return Json(new
        {
            success = true,
            roomRent = DisplayFormat.Money(b.RoomRent),
            electric = b.HasMeter ? $"{DisplayFormat.Number(b.ElectricUsage)} kWh × {DisplayFormat.Number(b.ElectricPrice)}" : "Không kèm điện nước",
            electricAmount = DisplayFormat.Money(b.ElectricAmount),
            water = b.HasMeter ? $"{DisplayFormat.Number(b.WaterUsage)} m³ × {DisplayFormat.Number(b.WaterPrice)}" : "Không kèm điện nước",
            waterAmount = DisplayFormat.Money(b.WaterAmount),
            serviceFee = DisplayFormat.Money(b.ServiceFee),
            adjustment = DisplayFormat.Money(b.Adjustment),
            total = DisplayFormat.Money(b.Total),
            message = result.Warning ?? ""
        });
    }

    /// <summary>
    /// Phát hành hóa đơn. Có điện nước thì bắt buộc phiếu chỉ số đủ 2 ảnh, và gọi RentalDocumentService.CaptureInvoiceAsync
    /// để lưu bộ chỉ số + ảnh riêng cho người thuê đối chiếu (yêu cầu ghép phía Landlord trong tài liệu của Tenant).
    /// Nếu lưu đối chiếu thất bại thì xóa hóa đơn vừa tạo: người thuê không bao giờ thấy hóa đơn thiếu bằng chứng.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(64 * 1024)]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] InvoiceInput input)
    {
        var ct = HttpContext.RequestAborted;
        var result = await ComputeAsync(input, null, collectErrors: false);
        ValidateDueDate(input);
        MeterReading? reading = null;
        if (ModelState.IsValid && result.ReadingId.HasValue)
        {
            reading = await Db.MeterReadings.AsNoTracking().FirstAsync(m => m.MeterReadingId == result.ReadingId.Value, ct);
            if (!MeterPhotoFiles.HasPhoto(reading.ElectricMeterImageUrl) || !MeterPhotoFiles.HasPhoto(reading.WaterMeterImageUrl))
                ModelState.AddModelError("Input.WithoutMeter",
                    "Phiếu chỉ số kỳ này chưa đủ ảnh công tơ điện và đồng hồ nước. Bổ sung ảnh ở phiếu chỉ số trước khi phát hành để người thuê đối chiếu.");
        }
        if (!ModelState.IsValid || result.Breakdown == null) return View("Create", await FormAsync(input, null, preview: false));

        var invoice = new Invoice
        {
            RentalContractId = input.RentalContractId, BillingPeriod = input.BillingPeriod, MeterReadingId = result.ReadingId,
            IssueDate = UtcNow, DueDate = input.DueDate!.Value.Date, PaymentStatus = PaymentStatus.Unpaid, CreatedAt = UtcNow
        };
        Fill(invoice, result.Breakdown, input);
        Db.Invoices.Add(invoice);
        try { await Db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Input.BillingPeriod", "Hợp đồng này đã có hóa đơn cho kỳ đã chọn.");
            return View("Create", await FormAsync(input, null, preview: false));
        }

        if (reading != null)
        {
            try
            {
                var electric = await MeterPhotoFiles.AsFormFileAsync(files, reading.ElectricMeterImageUrl, "cong-to-dien", ct);
                var water = await MeterPhotoFiles.AsFormFileAsync(files, reading.WaterMeterImageUrl, "dong-ho-nuoc", ct);
                await documents.CaptureInvoiceAsync(invoice.InvoiceId, CurrentUserId, electric, water, ct);
            }
            catch (Exception ex) when (ex is InvalidDataException or RentalDocumentConflictException or KeyNotFoundException or IOException)
            {
                Db.Invoices.Remove(invoice);
                await Db.SaveChangesAsync(CancellationToken.None);
                Log.LogWarning(ex, "Không lưu được bộ đối chiếu cho hóa đơn của chủ trọ {UserId}", CurrentUserId);
                ModelState.AddModelError("", "Chưa lưu được ảnh đối chiếu nên hóa đơn chưa được phát hành: " + ex.Message);
                return View("Create", await FormAsync(input, null, preview: false));
            }
        }

        var bank = await BankAccountStore.ReadAsync(Db, CurrentUserId);
        if (bank != null) invoice.VietQRContent = VietQr.Build(bank.Bin, bank.AccountNumber, invoice.TotalAmount, VietQr.Description(invoice.InvoiceId, invoice.BillingPeriod));
        TenantNotifier.Add(Db, result.TenantId!, $"Hóa đơn {VietnamTime.PeriodLabel(invoice.BillingPeriod)}",
            $"{result.RoomTitle}: {DisplayFormat.Money(invoice.TotalAmount)}, hạn thanh toán {VietnamTime.Date(invoice.DueDate)}.",
            TenantNotifier.InvoiceLink(invoice.InvoiceId), "invoice", UtcNow);
        await Db.SaveChangesAsync(ct);
        return Done("Đã phát hành hóa đơn và gửi thông báo cho người thuê." +
            (reading != null ? " Bộ chỉ số và ảnh công tơ đã được lưu để người thuê đối chiếu." : "") +
            (bank == null ? " Hóa đơn chưa có mã VietQR vì bạn chưa cài tài khoản nhận tiền." : ""),
            Back(nameof(Details), new { id = invoice.InvoiceId }));
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var i = await MyInvoices.AsNoTracking().Where(x => x.InvoiceId == id).Select(x => new
        {
            x, Room = x.RentalContract.RentalPost.Title,
            Address = x.RentalContract.RentalPost.Address + ", " + x.RentalContract.RentalPost.District + ", " + x.RentalContract.RentalPost.City,
            Tenant = x.RentalContract.Tenant.FullName, Landlord = x.RentalContract.Landlord.FullName,
            x.RentalContract.AgreedElectricityPrice, x.RentalContract.AgreedWaterPrice,
            Evidence = Db.InvoiceMeterEvidence.Where(e => e.InvoiceId == x.InvoiceId).Select(e => (DateTime?)e.CreatedAt).FirstOrDefault(),
            Reading = x.MeterReading == null ? null : new
            {
                x.MeterReading.PrevElectricIndex, x.MeterReading.CurrElectricIndex, x.MeterReading.PrevWaterIndex,
                x.MeterReading.CurrWaterIndex, x.MeterReading.ElectricMeterImageUrl, x.MeterReading.WaterMeterImageUrl
            }
        }).FirstOrDefaultAsync();
        if (i == null) return NotFoundPage("hóa đơn", Back(nameof(Index)));
        var png = VietQr.Png(i.x.VietQRContent);
        var model = new InvoiceDetailModel
        {
            Id = id, Period = i.x.BillingPeriod, Status = i.x.PaymentStatus, ContractId = i.x.RentalContractId,
            Overdue = (i.x.PaymentStatus == PaymentStatus.Unpaid || i.x.PaymentStatus == PaymentStatus.Overdue) && i.x.DueDate.Date < VietnamToday,
            RoomTitle = i.Room, RoomAddress = i.Address, TenantName = i.Tenant, LandlordName = i.Landlord,
            RoomRent = i.x.RoomRentAmount, ElectricUsage = i.x.ElectricUsage, ElectricAmount = i.x.ElectricAmount,
            WaterUsage = i.x.WaterUsage, WaterAmount = i.x.WaterAmount, ServiceFee = i.x.ServiceFeeAmount,
            Adjustment = i.x.AdjustmentAmount, Total = i.x.TotalAmount,
            ElectricPrice = i.AgreedElectricityPrice, WaterPrice = i.AgreedWaterPrice,
            IssueDate = i.x.IssueDate, DueDate = i.x.DueDate, TenantConfirmedAt = i.x.TenantConfirmedAt, PaidAt = i.x.PaidAt,
            Note = i.x.Note, HasQr = png != null, QrDataUri = png == null ? null : "data:image/png;base64," + Convert.ToBase64String(png),
            TransferNote = VietQr.Description(id, i.x.BillingPeriod), Bank = await BankAccountStore.ReadAsync(Db, CurrentUserId),
            MeterReadingId = i.x.MeterReadingId,
            PrevElectric = i.Reading?.PrevElectricIndex, CurrElectric = i.Reading?.CurrElectricIndex,
            PrevWater = i.Reading?.PrevWaterIndex, CurrWater = i.Reading?.CurrWaterIndex,
            HasEvidence = i.Evidence.HasValue, EvidenceCapturedAt = i.Evidence
        };
        return PageOrRegion("Details", "_InvoicePanel", model);
    }

    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] int id)
    {
        var invoice = await MyInvoices.AsNoTracking().FirstOrDefaultAsync(x => x.InvoiceId == id);
        if (invoice == null) return NotFoundPage("hóa đơn", Back(nameof(Index)));
        if (invoice.PaymentStatus != PaymentStatus.Unpaid || invoice.TenantConfirmedAt != null)
            return Fail(StatusCodes.Status409Conflict, "Chỉ sửa được hóa đơn chưa thanh toán và người thuê chưa báo đã chuyển tiền.", Back(nameof(Details), new { id }));
        var input = new InvoiceInput
        {
            RentalContractId = invoice.RentalContractId, BillingPeriod = invoice.BillingPeriod, DueDate = invoice.DueDate.Date,
            AdjustmentAmount = invoice.AdjustmentAmount, WithoutMeter = invoice.MeterReadingId == null,
            Note = UserNote(invoice.Note), IncludeFixedFee = true
        };
        return View("Edit", await FormAsync(input, id, preview: true));
    }

    [HttpPost]
    public async Task<IActionResult> Edit([FromRoute] int id, [Bind(Prefix = "Input")] InvoiceInput input)
    {
        var invoice = await MyInvoices.FirstOrDefaultAsync(x => x.InvoiceId == id);
        if (invoice == null) return NotFoundPage("hóa đơn", Back(nameof(Index)));
        if (invoice.PaymentStatus != PaymentStatus.Unpaid || invoice.TenantConfirmedAt != null)
            return Fail(StatusCodes.Status409Conflict, "Hóa đơn đã thay đổi trạng thái nên không sửa được.", Back(nameof(Details), new { id }));
        // Hợp đồng, kỳ và việc kèm điện nước của hóa đơn đã phát hành cố định (giữ khớp bộ đối chiếu đã lưu cho người thuê).
        input.RentalContractId = invoice.RentalContractId; input.BillingPeriod = invoice.BillingPeriod;
        input.WithoutMeter = invoice.MeterReadingId == null;
        var result = await ComputeAsync(input, id, collectErrors: false);
        ValidateDueDate(input);
        if (!ModelState.IsValid || result.Breakdown == null) return View("Edit", await FormAsync(input, id, preview: false));
        invoice.MeterReadingId = result.ReadingId;
        invoice.DueDate = input.DueDate!.Value.Date;
        Fill(invoice, result.Breakdown, input);
        var bank = await BankAccountStore.ReadAsync(Db, CurrentUserId);
        invoice.VietQRContent = bank == null ? null : VietQr.Build(bank.Bin, bank.AccountNumber, invoice.TotalAmount, VietQr.Description(id, invoice.BillingPeriod));
        TenantNotifier.Add(Db, result.TenantId!, $"Hóa đơn {VietnamTime.PeriodLabel(invoice.BillingPeriod)} đã được điều chỉnh",
            $"Số tiền mới: {DisplayFormat.Money(invoice.TotalAmount)}, hạn {VietnamTime.Date(invoice.DueDate)}. Hãy dùng mã QR mới nhất.",
            TenantNotifier.InvoiceLink(id), "invoice", UtcNow);
        await Db.SaveChangesAsync();
        return Done("Đã cập nhật hóa đơn và mã VietQR.", Back(nameof(Details), new { id }));
    }

    // ===================== Hỗ trợ =====================

    private static IQueryable<Invoice> Filter(IQueryable<Invoice> q, string key, DateTime today) => key switch
    {
        "open" => q.Where(i => i.PaymentStatus == PaymentStatus.Unpaid || i.PaymentStatus == PaymentStatus.AwaitingConfirmation || i.PaymentStatus == PaymentStatus.Overdue),
        "awaiting" => q.Where(i => i.PaymentStatus == PaymentStatus.AwaitingConfirmation),
        "overdue" => q.Where(i => (i.PaymentStatus == PaymentStatus.Unpaid || i.PaymentStatus == PaymentStatus.Overdue) && i.DueDate < today),
        "paid" => q.Where(i => i.PaymentStatus == PaymentStatus.Paid),
        "cancelled" => q.Where(i => i.PaymentStatus == PaymentStatus.Cancelled),
        _ => q
    };

    private sealed record Computed(InvoiceBreakdown? Breakdown, int? ReadingId, string? TenantId, string? RoomTitle, string? Error, string? Warning);

    /// <summary>Một hàm tính duy nhất cho xem trước (AJAX) và lưu. Chỉ dùng hợp đồng Active của chính chủ trọ.</summary>
    private async Task<Computed> ComputeAsync(InvoiceInput input, int? invoiceId, bool collectErrors)
    {
        string? firstError = null;
        void Error(string key, string message) { firstError ??= message; if (!collectErrors) ModelState.AddModelError(key, message); }

        var contract = await MyContracts.AsNoTracking().Include(c => c.RentalPost).ThenInclude(p => p.Services)
            .FirstOrDefaultAsync(c => c.RentalContractId == input.RentalContractId);
        if (contract == null || (contract.Status != ContractStatus.Active && invoiceId == null))
        { Error("Input.RentalContractId", "Chọn hợp đồng đang hiệu lực của bạn."); return new(null, null, null, null, firstError, null); }
        if (!VietnamTime.TryParsePeriod(input.BillingPeriod, out var first))
        { Error("Input.BillingPeriod", "Kỳ thanh toán không hợp lệ."); return new(null, null, null, null, firstError, null); }
        if (first.AddMonths(1) <= contract.StartDate.Date || first > contract.EndDate.Date)
            Error("Input.BillingPeriod", $"Kỳ thanh toán nằm ngoài thời hạn hợp đồng ({VietnamTime.Date(contract.StartDate)} – {VietnamTime.Date(contract.EndDate)}).");
        if (string.CompareOrdinal(input.BillingPeriod, VietnamTime.Period(VietnamNow.AddMonths(1))) > 0)
            Error("Input.BillingPeriod", "Chỉ lập hóa đơn đến tháng sau.");
        if (await Db.Invoices.AnyAsync(i => i.RentalContractId == contract.RentalContractId && i.BillingPeriod == input.BillingPeriod && i.InvoiceId != (invoiceId ?? 0)))
            Error("Input.BillingPeriod", "Hợp đồng này đã có hóa đơn cho kỳ đã chọn.");
        if (input.AdjustmentAmount is < -100_000_000 or > 100_000_000)
            Error("Input.AdjustmentAmount", "Khoản điều chỉnh từ -100.000.000 đến 100.000.000 đ.");

        MeterReading? reading = null;
        string? warning = null;
        if (!input.WithoutMeter)
        {
            reading = await Db.MeterReadings.AsNoTracking()
                .FirstOrDefaultAsync(m => m.RentalPostId == contract.RentalPostId && m.Period == input.BillingPeriod);
            if (reading == null)
                Error("Input.WithoutMeter", $"Chưa ghi chỉ số điện nước {VietnamTime.PeriodLabel(input.BillingPeriod)}. Hãy ghi chỉ số trước hoặc chọn “Lập hóa đơn không kèm điện nước”.");
            else if (await Db.Invoices.AnyAsync(i => i.MeterReadingId == reading.MeterReadingId && i.PaymentStatus != PaymentStatus.Cancelled && i.InvoiceId != (invoiceId ?? 0)))
                Error("Input.WithoutMeter", "Chỉ số kỳ này đã được dùng ở một hóa đơn khác.");
        }

        var services = new List<ServiceCharge>();
        foreach (var s in contract.RentalPost.Services.OrderBy(s => s.ServiceId))
        {
            var qty = input.Qty.GetValueOrDefault(s.ServiceId);
            if (qty < 0 || qty > 20) { Error($"Input.Qty[{s.ServiceId}]", $"Số lượng “{s.Name}” từ 0 đến 20."); continue; }
            if (qty > 0) services.Add(new ServiceCharge(s.Name, s.UnitPrice, qty, s.Unit));
        }
        var breakdown = BillingCalculator.Compute(contract, reading, input.IncludeFixedFee ? contract.RentalPost.ServiceFee : 0,
            services, input.AdjustmentAmount ?? 0);
        if (breakdown.Total < 0) Error("Input.AdjustmentAmount", "Tổng tiền không được âm.");
        if (breakdown.Total > 10_000_000_000m) Error("Input.AdjustmentAmount", "Tổng tiền vượt giới hạn.");
        if (firstError != null && !collectErrors) return new(null, null, contract.TenantId, contract.RentalPost.Title, firstError, null);
        if (firstError != null) return new(breakdown, reading?.MeterReadingId, contract.TenantId, contract.RentalPost.Title, firstError, null);
        if (input.WithoutMeter) warning = "Hóa đơn không tính tiền điện nước.";
        return new(breakdown, reading?.MeterReadingId, contract.TenantId, contract.RentalPost.Title, null, warning);
    }

    private void ValidateDueDate(InvoiceInput input)
    {
        if (input.DueDate.HasValue && (input.DueDate.Value.Date < VietnamToday || input.DueDate.Value.Date > VietnamToday.AddDays(60)))
            ModelState.AddModelError("Input.DueDate", "Hạn thanh toán từ hôm nay đến 60 ngày tới.");
    }

    private static void Fill(Invoice invoice, InvoiceBreakdown b, InvoiceInput input)
    {
        invoice.RoomRentAmount = b.RoomRent;
        invoice.ElectricUsage = b.ElectricUsage; invoice.ElectricAmount = b.ElectricAmount;
        invoice.WaterUsage = b.WaterUsage; invoice.WaterAmount = b.WaterAmount;
        invoice.ServiceFeeAmount = b.ServiceFee; invoice.AdjustmentAmount = b.Adjustment; invoice.TotalAmount = b.Total;
        // Ghi chú = diễn giải dịch vụ (để người thuê đối chiếu) + lời nhắn của chủ trọ, tối đa 500 ký tự như model.
        var note = string.Join(" ", new[] { b.ServiceNote(), string.IsNullOrWhiteSpace(input.Note) ? null : "Ghi chú: " + input.Note.Trim() }
            .Where(x => !string.IsNullOrEmpty(x)));
        invoice.Note = DisplayFormat.Clean(note, 500);
    }

    private static string? UserNote(string? note)
    {
        if (string.IsNullOrEmpty(note)) return null;
        var index = note.IndexOf("Ghi chú: ", StringComparison.Ordinal);
        return index < 0 ? null : note[(index + 9)..];
    }

    private async Task<InvoiceFormModel> FormAsync(InvoiceInput input, int? id, bool preview)
    {
        var contracts = await MyContracts.AsNoTracking()
            .Where(c => c.Status == ContractStatus.Active || c.RentalContractId == input.RentalContractId)
            .OrderBy(c => c.RentalPost.Title)
            .Select(c => new OptionItem(c.RentalContractId.ToString(), c.RentalPost.Title + " – " + c.Tenant.FullName, null)).ToListAsync();
        var selected = await MyContracts.AsNoTracking().Where(c => c.RentalContractId == input.RentalContractId)
            .Select(c => new
            {
                c.MonthlyRent, c.AgreedElectricityPrice, c.AgreedWaterPrice, c.RentalPost.ServiceFee,
                Services = c.RentalPost.Services.OrderBy(s => s.ServiceId).Select(s => new ServiceOption(s.ServiceId, s.Name, s.UnitPrice, s.Unit)).ToList()
            }).FirstOrDefaultAsync();
        if (selected != null && input.Qty.Count == 0 && preview)
            foreach (var s in selected.Services.Where(s => s.Unit.Contains("tháng", StringComparison.OrdinalIgnoreCase)))
                input.Qty[s.Id] = 1;
        InvoiceBreakdown? breakdown = null; int? readingId = null;
        if (preview && selected != null)
        {
            var computed = await ComputeAsync(input, id, collectErrors: true);
            breakdown = computed.Breakdown; readingId = computed.ReadingId;
        }
        var photosReady = false;
        if (selected != null)
        {
            var photos = await MyContracts.Where(c => c.RentalContractId == input.RentalContractId)
                .SelectMany(c => c.RentalPost.MeterReadings.Where(m => m.Period == input.BillingPeriod))
                .Select(m => new { m.ElectricMeterImageUrl, m.WaterMeterImageUrl }).FirstOrDefaultAsync();
            photosReady = photos != null && MeterPhotoFiles.HasPhoto(photos.ElectricMeterImageUrl) && MeterPhotoFiles.HasPhoto(photos.WaterMeterImageUrl);
        }
        return new InvoiceFormModel
        {
            Id = id, Input = input, Contracts = contracts, Services = selected?.Services ?? [], FixedFee = selected?.ServiceFee ?? 0,
            Breakdown = breakdown, MeterReadingId = readingId,
            ContractSummary = selected == null ? null
                : $"Tiền phòng {DisplayFormat.Money(selected.MonthlyRent)} · điện {DisplayFormat.Number(selected.AgreedElectricityPrice)} đ/kWh · nước {DisplayFormat.Number(selected.AgreedWaterPrice)} đ/m³",
            BankConfigured = await BankAccountStore.ReadAsync(Db, CurrentUserId) != null,
            MeterPhotosReady = photosReady
        };
    }
}
