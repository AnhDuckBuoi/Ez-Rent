using EZ_Rent.Areas.Landlord.Services;
using EZ_Rent.Areas.Landlord.ViewModels;
using EZ_Rent.Data;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>Tài khoản nhận tiền dùng để sinh mã VietQR trên hóa đơn.</summary>
public sealed class PaymentSettingsController(AppDbContext db, TimeProvider? clock = null) : LandlordControllerBase(db, clock)
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var bank = await LandlordBankAccounts.ReadAsync(Db, CurrentUserId);
        return View("Index", new BankInput { Bin = bank?.Bin ?? "", AccountNumber = bank?.AccountNumber ?? "", AccountName = bank?.AccountName ?? "" });
    }

    [HttpPost]
    public async Task<IActionResult> Index(BankInput input)
    {
        input.AccountNumber = (input.AccountNumber ?? "").Replace(" ", "");
        input.AccountName = LandlordFormat.Ascii(input.AccountName).ToUpperInvariant();
        ModelState.Clear();
        TryValidateModel(input);
        if (!LandlordBankAccounts.IsKnownBin(input.Bin)) ModelState.AddModelError(nameof(input.Bin), "Chọn ngân hàng trong danh sách.");
        if (string.IsNullOrWhiteSpace(input.AccountName)) ModelState.AddModelError(nameof(input.AccountName), "Tên chủ tài khoản chỉ gồm chữ cái và khoảng trắng.");
        if (!ModelState.IsValid) return View("Index", input);
        await LandlordBankAccounts.SaveAsync(Db, CurrentUserId, new LandlordBank(input.Bin, input.AccountNumber, input.AccountName));
        return Done("Đã lưu tài khoản nhận tiền. Hóa đơn mới sẽ có mã VietQR; hóa đơn cũ dùng nút “Tạo lại mã QR”.", Back(nameof(Index)));
    }
}
