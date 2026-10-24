using System.Globalization;
using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Services.Contracts;

/// <summary>Thông tin "Bên cho thuê" trên mẫu BM03 mà phía Tenant ghi là còn thiếu nguồn nhập.</summary>
public sealed record LandlordLegalProfile(DateOnly? DateOfBirth, IdentityDocumentKind? DocumentKind, string? DocumentNumber,
    DateOnly? DocumentIssuedOn, string? DocumentIssuedAt, string? PermanentAddress)
{
    public bool IsEmpty => DateOfBirth == null && DocumentKind == null && string.IsNullOrWhiteSpace(DocumentNumber) &&
        DocumentIssuedOn == null && string.IsNullOrWhiteSpace(DocumentIssuedAt) && string.IsNullOrWhiteSpace(PermanentAddress);
}

/// <summary>
/// Lưu thông tin pháp lý của chủ trọ cho BM03 (ngày sinh, giấy tờ, thường trú). Không thêm bảng/migration:
/// mỗi mục là một claim trong AspNetUserClaims, chỉ đọc theo UserId của chính chủ trọ.
/// Phía Tenant có thể đọc cùng các claim này để điền phần Bên A trên bản nháp BM03 (khóa nằm trong các hằng số dưới).
/// </summary>
public static class LandlordLegalProfileStore
{
    public const string DateOfBirthClaim = "ezrent:landlord:bm03:date-of-birth";
    public const string DocumentKindClaim = "ezrent:landlord:bm03:document-kind";
    public const string DocumentNumberClaim = "ezrent:landlord:bm03:document-number";
    public const string IssuedOnClaim = "ezrent:landlord:bm03:document-issued-on";
    public const string IssuedAtClaim = "ezrent:landlord:bm03:document-issued-at";
    public const string AddressClaim = "ezrent:landlord:bm03:permanent-address";
    private static readonly string[] All = [DateOfBirthClaim, DocumentKindClaim, DocumentNumberClaim, IssuedOnClaim, IssuedAtClaim, AddressClaim];

    public static async Task<LandlordLegalProfile> ReadAsync(AppDbContext db, string landlordId, CancellationToken ct = default)
    {
        var claims = await db.UserClaims.AsNoTracking().Where(c => c.UserId == landlordId && All.Contains(c.ClaimType!))
            .Select(c => new { c.ClaimType, c.ClaimValue }).ToListAsync(ct);
        string? Get(string type) => claims.Where(c => c.ClaimType == type).Select(c => c.ClaimValue).FirstOrDefault();
        static DateOnly? Day(string? v) => DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
        IdentityDocumentKind? kind = Enum.TryParse<IdentityDocumentKind>(Get(DocumentKindClaim), out var k) && Enum.IsDefined(k) ? k : null;
        return new LandlordLegalProfile(Day(Get(DateOfBirthClaim)), kind, Get(DocumentNumberClaim), Day(Get(IssuedOnClaim)),
            Get(IssuedAtClaim), Get(AddressClaim));
    }

    public static async Task SaveAsync(AppDbContext db, string landlordId, LandlordLegalProfile profile, CancellationToken ct = default)
    {
        var old = await db.UserClaims.Where(c => c.UserId == landlordId && All.Contains(c.ClaimType!)).ToListAsync(ct);
        db.UserClaims.RemoveRange(old);
        void Add(string type, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                db.UserClaims.Add(new IdentityUserClaim<string> { UserId = landlordId, ClaimType = type, ClaimValue = value.Trim() });
        }
        Add(DateOfBirthClaim, profile.DateOfBirth?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(DocumentKindClaim, profile.DocumentKind?.ToString());
        Add(DocumentNumberClaim, profile.DocumentNumber);
        Add(IssuedOnClaim, profile.DocumentIssuedOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Add(IssuedAtClaim, profile.DocumentIssuedAt);
        Add(AddressClaim, profile.PermanentAddress);
        await db.SaveChangesAsync(ct);
    }
}
