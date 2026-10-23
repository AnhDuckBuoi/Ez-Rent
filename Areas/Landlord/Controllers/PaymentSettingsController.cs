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
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>Tài khoản nhận tiền dùng để sinh mã VietQR trên hóa đơn.</summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class PaymentSettingsController(AppDbContext db, TimeProvider? clock = null) : LandlordAreaController(db, clock)
{
    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index()
    {
        var bank = await BankAccountStore.ReadAsync(Db, CurrentUserId);
        return View("Index", new BankInput { Bin = bank?.Bin ?? "", AccountNumber = bank?.AccountNumber ?? "", AccountName = bank?.AccountName ?? "" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(BankInput input)
    {
        input.AccountNumber = (input.AccountNumber ?? "").Replace(" ", "");
        input.AccountName = DisplayFormat.Ascii(input.AccountName).ToUpperInvariant();
        ModelState.Clear();
        TryValidateModel(input);
        if (!BankAccountStore.IsKnownBin(input.Bin)) ModelState.AddModelError(nameof(input.Bin), "Chọn ngân hàng trong danh sách.");
        if (string.IsNullOrWhiteSpace(input.AccountName)) ModelState.AddModelError(nameof(input.AccountName), "Tên chủ tài khoản chỉ gồm chữ cái và khoảng trắng.");
        if (!ModelState.IsValid) return View("Index", input);
        await BankAccountStore.SaveAsync(Db, CurrentUserId, new BankAccount(input.Bin, input.AccountNumber, input.AccountName));
        return Done("Đã lưu tài khoản nhận tiền. Hóa đơn mới sẽ có mã VietQR; hóa đơn cũ dùng nút “Tạo lại mã QR”.", Back(nameof(Index)));
    }
}
