using EZ_Rent.Areas.Landlord.Helpers;
using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services.Common;
using EZ_Rent.Services.Notifications;
using EZ_Rent.Services.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC21 Theo dõi trạng thái thanh toán (P07, R38–R39): xác nhận đã nhận tiền (bước 2 của thanh toán 2 bước),
/// hủy hóa đơn lập sai, tạo lại mã VietQR khi đổi tài khoản nhận tiền.
/// Mọi cập nhật kèm điều kiện trạng thái trong câu UPDATE nên không ghi đè thay đổi vừa xảy ra.
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class InvoicePaymentsController(AppDbContext db, TimeProvider? clock = null) : LandlordAreaController(db, clock)
{
    private string InvoicePage(int id) => Url.Action("Details", "Invoices", new { id }) ?? "/Landlord/Invoices";
    private string InvoiceList => Url.Action("Index", "Invoices") ?? "/Landlord/Invoices";

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkPaid([FromRoute] int id)
    {
        var back = InvoicePage(id);
        var invoice = await MyInvoices.AsNoTracking().Include(x => x.RentalContract).ThenInclude(c => c.RentalPost)
            .FirstOrDefaultAsync(x => x.InvoiceId == id);
        if (invoice == null) return NotFoundPage("hóa đơn", InvoiceList);
        var changed = await Db.Invoices.Where(x => x.InvoiceId == id &&
                (x.PaymentStatus == PaymentStatus.Unpaid || x.PaymentStatus == PaymentStatus.AwaitingConfirmation || x.PaymentStatus == PaymentStatus.Overdue))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PaymentStatus, PaymentStatus.Paid)
                .SetProperty(x => x.PaidAt, UtcNow).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Hóa đơn đã thanh toán hoặc đã hủy.", back);
        TenantNotifier.Add(Db, invoice.RentalContract.TenantId, $"Đã nhận tiền hóa đơn {VietnamTime.PeriodLabel(invoice.BillingPeriod)}",
            $"{invoice.RentalContract.RentalPost.Title}: chủ nhà xác nhận đã nhận {DisplayFormat.Money(invoice.TotalAmount)}.",
            TenantNotifier.InvoiceLink(id), "invoice", UtcNow);
        await Db.SaveChangesAsync();
        return Done("Đã xác nhận nhận tiền. Hóa đơn chuyển sang “Đã thanh toán”.", back);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel([FromRoute] int id)
    {
        var back = InvoicePage(id);
        var invoice = await MyInvoices.AsNoTracking().Include(x => x.RentalContract).FirstOrDefaultAsync(x => x.InvoiceId == id);
        if (invoice == null) return NotFoundPage("hóa đơn", InvoiceList);
        var changed = await Db.Invoices.Where(x => x.InvoiceId == id && x.TenantConfirmedAt == null &&
                (x.PaymentStatus == PaymentStatus.Unpaid || x.PaymentStatus == PaymentStatus.Overdue))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.PaymentStatus, PaymentStatus.Cancelled).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Chỉ hủy được hóa đơn chưa thanh toán và người thuê chưa báo đã chuyển tiền.", back);
        TenantNotifier.Add(Db, invoice.RentalContract.TenantId, $"Hóa đơn {VietnamTime.PeriodLabel(invoice.BillingPeriod)} đã bị hủy",
            "Bạn không cần thanh toán hóa đơn này. Liên hệ chủ nhà nếu có thắc mắc.", TenantNotifier.InvoiceLink(id), "invoice", UtcNow);
        await Db.SaveChangesAsync();
        return Done("Đã hủy hóa đơn. Chỉ số điện nước của kỳ có thể sửa lại nếu cần.", back);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshQr([FromRoute] int id)
    {
        var back = InvoicePage(id);
        var invoice = await MyInvoices.FirstOrDefaultAsync(x => x.InvoiceId == id);
        if (invoice == null) return NotFoundPage("hóa đơn", InvoiceList);
        if (invoice.PaymentStatus is PaymentStatus.Paid or PaymentStatus.Cancelled)
            return Fail(StatusCodes.Status409Conflict, "Hóa đơn đã thanh toán hoặc đã hủy.", back);
        var bank = await BankAccountStore.ReadAsync(Db, CurrentUserId);
        if (bank == null) return Fail(StatusCodes.Status400BadRequest, "Bạn chưa cài tài khoản nhận tiền.", Back("Index", new { controller = "PaymentSettings" }));
        invoice.VietQRContent = VietQr.Build(bank.Bin, bank.AccountNumber, invoice.TotalAmount, VietQr.Description(id, invoice.BillingPeriod));
        await Db.SaveChangesAsync();
        return Done("Đã tạo lại mã VietQR theo tài khoản nhận tiền hiện tại.", back);
    }
}
