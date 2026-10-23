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
using EZ_Rent.Services.Contracts;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC27 Quản lý thông tin tài khoản (phía chủ trọ) + R55 đổi mật khẩu khi đã đăng nhập
/// + thông tin pháp lý "Bên cho thuê" cho hợp đồng BM03.
/// Hồ sơ, ảnh đại diện và mật khẩu nằm ngay trong giao diện chủ trọ; email, 2FA, đăng nhập Google
/// dùng các trang Identity sẵn có của nhóm (cùng cơ chế với phần Tenant).
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class AccountController(AppDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    IWebHostEnvironment env, TimeProvider? clock = null) : LandlordAreaController(db, clock)
{
    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        return View("Index", await ModelAsync(user, null, null));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile([Bind(Prefix = "Profile")] LandlordProfileInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        if (!ModelState.IsValid) return View("Index", await ModelAsync(user, input, null));
        user.FullName = input.FullName.Trim();
        var phone = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        if (phone != user.PhoneNumber) user.PhoneNumberConfirmed = false;
        user.PhoneNumber = phone;
        user.UpdatedAt = UtcNow;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError("Profile", e.Description);
            return View("Index", await ModelAsync(user, input, null));
        }
        await signIn.RefreshSignInAsync(user);
        return Done("Đã lưu thông tin tài khoản.", Back(nameof(Index)));
    }

    [HttpPost]
    [RequestSizeLimit(UploadStorage.MaxAvatarBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = UploadStorage.MaxAvatarBytes + 65536)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Avatar(IFormFile? file)
    {
        var back = Back(nameof(Index));
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        var (upload, error) = await UploadStorage.CheckAsync(file, allowVideo: false, UploadStorage.MaxAvatarBytes, HttpContext.RequestAborted);
        if (error != null) return Fail(StatusCodes.Status400BadRequest, error, back);

        var oldUrl = user.AvatarUrl;
        user.AvatarUrl = await UploadStorage.SaveAvatarAsync(env, upload!, HttpContext.RequestAborted);
        user.UpdatedAt = UtcNow;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            UploadStorage.TryDelete(env, user.AvatarUrl);
            return Fail(StatusCodes.Status400BadRequest, "Chưa lưu được ảnh đại diện. Hãy thử lại.", back);
        }
        UploadStorage.TryDelete(env, oldUrl);
        return Done("Đã cập nhật ảnh đại diện.", back);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvatar()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        var oldUrl = user.AvatarUrl;
        user.AvatarUrl = null;
        user.UpdatedAt = UtcNow;
        await users.UpdateAsync(user);
        UploadStorage.TryDelete(env, oldUrl);
        return Done("Đã bỏ ảnh đại diện.", Back(nameof(Index)));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Password([Bind(Prefix = "Password")] LandlordPasswordInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        if (!await users.HasPasswordAsync(user))
            return Fail(StatusCodes.Status400BadRequest, "Tài khoản đăng nhập bằng Google chưa có mật khẩu. Hãy đặt mật khẩu ở trang bảo mật của tài khoản.", Back(nameof(Index)));
        if (!ModelState.IsValid) return View("Index", await ModelAsync(user, null, input));
        var result = await users.ChangePasswordAsync(user, input.CurrentPassword, input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError("Password", e.Code == "PasswordMismatch" ? "Mật khẩu hiện tại không đúng." : e.Description);
            return View("Index", await ModelAsync(user, null, new LandlordPasswordInput()));
        }
        // Đổi security stamp → các phiên đăng nhập cũ hết hiệu lực; làm mới phiên hiện tại.
        await signIn.RefreshSignInAsync(user);
        return Done("Đã đổi mật khẩu.", Back(nameof(Index)));
    }

    /// <summary>Thông tin "Bên cho thuê" trên BM03 (phía Tenant ghi là chưa có nguồn nhập). Lưu bằng claim, không đổi schema.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Legal([Bind(Prefix = "Legal")] LegalProfileInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        var today = DateOnly.FromDateTime(VietnamToday);
        DateOnly? Day(DateTime? v) => v.HasValue ? DateOnly.FromDateTime(v.Value) : null;
        var born = Day(input.DateOfBirth); var issued = Day(input.DocumentIssuedOn);
        if (born.HasValue && (born > today.AddYears(-18) || born < today.AddYears(-110)))
            ModelState.AddModelError("Legal.DateOfBirth", "Ngày sinh không hợp lệ (chủ trọ phải đủ 18 tuổi).");
        if (issued.HasValue && (issued > today || (born.HasValue && issued < born)))
            ModelState.AddModelError("Legal.DocumentIssuedOn", "Ngày cấp không ở tương lai và không trước ngày sinh.");
        if (input.DocumentKind.HasValue && !Enum.IsDefined(input.DocumentKind.Value))
            ModelState.AddModelError("Legal.DocumentKind", "Chọn loại giấy tờ trong danh sách.");
        if (!ModelState.IsValid) return View("Index", await ModelAsync(user, null, null, input));
        await LandlordLegalProfileStore.SaveAsync(Db, user.Id, new LandlordLegalProfile(born, input.DocumentKind,
            DisplayFormat.Clean(input.DocumentNumber, 30), issued, DisplayFormat.Clean(input.DocumentIssuedAt, 200),
            DisplayFormat.Clean(input.PermanentAddress, 500)));
        return Done("Đã lưu thông tin pháp lý dùng cho hợp đồng BM03.", Back(nameof(Index)) + "#legal");
    }

    private async Task<LandlordAccountModel> ModelAsync(ApplicationUser user, LandlordProfileInput? profile, LandlordPasswordInput? password,
        LegalProfileInput? legal = null)
    {
        var saved = await LandlordLegalProfileStore.ReadAsync(Db, user.Id);
        return new LandlordAccountModel
    {
        Legal = legal ?? new LegalProfileInput
        {
            DateOfBirth = saved.DateOfBirth?.ToDateTime(TimeOnly.MinValue), DocumentKind = saved.DocumentKind, DocumentNumber = saved.DocumentNumber,
            DocumentIssuedOn = saved.DocumentIssuedOn?.ToDateTime(TimeOnly.MinValue), DocumentIssuedAt = saved.DocumentIssuedAt,
            PermanentAddress = saved.PermanentAddress
        },
        Profile = profile ?? new LandlordProfileInput { FullName = user.FullName, PhoneNumber = user.PhoneNumber },
        Password = password ?? new LandlordPasswordInput(),
        Email = user.Email ?? "", EmailConfirmed = user.EmailConfirmed, AvatarUrl = DisplayFormat.SafeMediaUrl(user.AvatarUrl),
        CreatedAt = user.CreatedAt, HasPassword = await users.HasPasswordAsync(user), TwoFactorEnabled = user.TwoFactorEnabled
    };
    }
}
