using EZ_Rent.Areas.Landlord.Services;
using EZ_Rent.Areas.Landlord.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC27 Quản lý thông tin tài khoản (phía chủ trọ) + R55 đổi mật khẩu khi đã đăng nhập.
/// Hồ sơ, ảnh đại diện và mật khẩu nằm ngay trong giao diện chủ trọ; email, 2FA, đăng nhập Google
/// dùng các trang Identity sẵn có của nhóm (cùng cơ chế với phần Tenant).
/// </summary>
public sealed class AccountController(AppDbContext db, UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    IWebHostEnvironment env, TimeProvider? clock = null) : LandlordControllerBase(db, clock)
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        return View("Index", await ModelAsync(user, null, null));
    }

    [HttpPost]
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
    [RequestSizeLimit(LandlordUploads.MaxAvatarBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = LandlordUploads.MaxAvatarBytes + 65536)]
    public async Task<IActionResult> Avatar(IFormFile? file)
    {
        var back = Back(nameof(Index));
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        var (upload, error) = await LandlordUploads.CheckAsync(file, allowVideo: false, HttpContext.RequestAborted);
        if (error != null) return Fail(StatusCodes.Status400BadRequest, error, back);
        if (upload!.File.Length > LandlordUploads.MaxAvatarBytes) return Fail(StatusCodes.Status400BadRequest, "Ảnh đại diện tối đa 2 MB.", back);
        var oldUrl = user.AvatarUrl;
        user.AvatarUrl = await LandlordUploads.SaveAvatarAsync(env, upload, HttpContext.RequestAborted);
        user.UpdatedAt = UtcNow;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            LandlordUploads.TryDelete(env, user.AvatarUrl);
            return Fail(StatusCodes.Status400BadRequest, "Chưa lưu được ảnh đại diện. Hãy thử lại.", back);
        }
        LandlordUploads.TryDelete(env, oldUrl);
        return Done("Đã cập nhật ảnh đại diện.", back);
    }

    [HttpPost]
    public async Task<IActionResult> RemoveAvatar()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return Challenge();
        var oldUrl = user.AvatarUrl;
        user.AvatarUrl = null;
        user.UpdatedAt = UtcNow;
        await users.UpdateAsync(user);
        LandlordUploads.TryDelete(env, oldUrl);
        return Done("Đã bỏ ảnh đại diện.", Back(nameof(Index)));
    }

    [HttpPost]
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

    private async Task<LandlordAccountModel> ModelAsync(ApplicationUser user, LandlordProfileInput? profile, LandlordPasswordInput? password) => new()
    {
        Profile = profile ?? new LandlordProfileInput { FullName = user.FullName, PhoneNumber = user.PhoneNumber },
        Password = password ?? new LandlordPasswordInput(),
        Email = user.Email ?? "", EmailConfirmed = user.EmailConfirmed, AvatarUrl = LandlordFormat.SafeMediaUrl(user.AvatarUrl),
        CreatedAt = user.CreatedAt, HasPassword = await users.HasPasswordAsync(user), TwoFactorEnabled = user.TwoFactorEnabled
    };
}
