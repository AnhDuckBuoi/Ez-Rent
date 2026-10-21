using EZ_Rent.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Services;

public sealed record LandlordBank(string Bin, string AccountNumber, string AccountName)
{
    public string BankName => LandlordBankAccounts.Banks.FirstOrDefault(b => b.Bin == Bin).Name ?? Bin;
}

/// <summary>
/// Tài khoản nhận tiền của chủ trọ. Không thêm cột/migration: lưu vào bảng AspNetUserClaims sẵn có của Identity
/// (mỗi thông tin một claim, chỉ đọc theo UserId của chính chủ trọ).
/// </summary>
public static class LandlordBankAccounts
{
    public const string BinClaim = "ezrent:landlord:bank-bin";
    public const string AccountClaim = "ezrent:landlord:bank-account";
    public const string NameClaim = "ezrent:landlord:bank-account-name";

    // Mã BIN theo danh sách ngân hàng hỗ trợ VietQR của NAPAS.
    public static readonly IReadOnlyList<(string Bin, string Name)> Banks =
    [
        ("970436", "Vietcombank"), ("970415", "VietinBank"), ("970418", "BIDV"), ("970405", "Agribank"),
        ("970407", "Techcombank"), ("970422", "MB Bank"), ("970416", "ACB"), ("970432", "VPBank"),
        ("970423", "TPBank"), ("970403", "Sacombank"), ("970441", "VIB"), ("970443", "SHB"),
        ("970437", "HDBank"), ("970448", "OCB"), ("970426", "MSB"), ("970440", "SeABank"),
        ("970431", "Eximbank"), ("970449", "LPBank"), ("970428", "Nam A Bank"), ("970425", "ABBANK")
    ];

    public static bool IsKnownBin(string? bin) => Banks.Any(b => b.Bin == bin);

    public static async Task<LandlordBank?> ReadAsync(AppDbContext db, string userId, CancellationToken ct = default)
    {
        var claims = await db.UserClaims.AsNoTracking()
            .Where(c => c.UserId == userId && (c.ClaimType == BinClaim || c.ClaimType == AccountClaim || c.ClaimType == NameClaim))
            .Select(c => new { c.ClaimType, c.ClaimValue }).ToListAsync(ct);
        string? Get(string type) => claims.Where(c => c.ClaimType == type).Select(c => c.ClaimValue).FirstOrDefault();
        var bin = Get(BinClaim); var account = Get(AccountClaim); var name = Get(NameClaim);
        return string.IsNullOrEmpty(bin) || string.IsNullOrEmpty(account) || !IsKnownBin(bin)
            ? null : new LandlordBank(bin, account, name ?? "");
    }

    public static async Task SaveAsync(AppDbContext db, string userId, LandlordBank bank, CancellationToken ct = default)
    {
        var old = await db.UserClaims
            .Where(c => c.UserId == userId && (c.ClaimType == BinClaim || c.ClaimType == AccountClaim || c.ClaimType == NameClaim))
            .ToListAsync(ct);
        db.UserClaims.RemoveRange(old);
        db.UserClaims.AddRange(
            new IdentityUserClaim<string> { UserId = userId, ClaimType = BinClaim, ClaimValue = bank.Bin },
            new IdentityUserClaim<string> { UserId = userId, ClaimType = AccountClaim, ClaimValue = bank.AccountNumber },
            new IdentityUserClaim<string> { UserId = userId, ClaimType = NameClaim, ClaimValue = bank.AccountName });
        await db.SaveChangesAsync(ct);
    }
}
