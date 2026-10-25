using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace EZ_Rent.Areas.Tenant.Controllers;

[Area("Tenant")]
[Authorize(Roles = "Tenant")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AccountController(AppDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, IWebHostEnvironment environment, ILogger<AccountController> logger)
    : TenantControllerBase(db)
{
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await users.GetUserAsync(User);
        return user == null ? NotFound() : View(Profile(user));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProfile([Bind(Prefix = "Profile")] ProfileInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return NotFound();
        if (!ModelState.IsValid) return View("Index", Profile(user, input));

        // Only these two submitted fields are editable; identity, email and roles stay server-owned.
        user.FullName = input.FullName.Trim();
        var phone = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim();
        if (phone != user.PhoneNumber) user.PhoneNumberConfirmed = false;
        user.PhoneNumber = phone;
        user.UpdatedAt = DateTime.UtcNow;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View("Index", Profile(user, input));
        }

        await signIn.RefreshSignInAsync(user);
        TempData["DiscoveryMessage"] = "Đã cập nhật thông tin cá nhân.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    [RequestSizeLimit(TenantAvatarStorage.MaxBytes + 65536)]
    [RequestFormLimits(MultipartBodyLengthLimit = TenantAvatarStorage.MaxBytes + 65536)]
    public async Task<IActionResult> UploadAvatar([Bind(Prefix = "Avatar")] AvatarInput input, CancellationToken ct)
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return NotFound();
        if (!ModelState.IsValid) return View("Index", Profile(user));
        string newUrl;
        try
        {
            newUrl = await TenantAvatarStorage.SaveAsync(input.File!, environment.WebRootPath, ct);
        }
        catch (InvalidDataException ex)
        {
            ModelState.AddModelError("Avatar.File", ex.Message);
            return View("Index", Profile(user));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Không lưu được ảnh đại diện.");
            ModelState.AddModelError("Avatar.File", "Chưa lưu được ảnh. Hãy thử lại.");
            return View("Index", Profile(user));
        }

        var oldUrl = user.AvatarUrl;
        user.AvatarUrl = newUrl;
        user.UpdatedAt = DateTime.UtcNow;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            DeleteAvatar(newUrl);
            user.AvatarUrl = oldUrl;
            AddErrors(result);
            return View("Index", Profile(user));
        }
        DeleteAvatar(oldUrl);
        TempData["DiscoveryMessage"] = "Đã cập nhật ảnh đại diện.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvatar()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return NotFound();
        var oldUrl = user.AvatarUrl;
        user.AvatarUrl = null;
        user.UpdatedAt = DateTime.UtcNow;
        var result = await users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            user.AvatarUrl = oldUrl;
            AddErrors(result);
            return View("Index", Profile(user));
        }
        DeleteAvatar(oldUrl);
        TempData["DiscoveryMessage"] = "Đã gỡ ảnh đại diện.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Security()
    {
        var user = await users.GetUserAsync(User);
        return user == null ? NotFound() : View(await SecurityModel(user));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdatePassword([Bind(Prefix = "Password")] PasswordInput input)
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return NotFound();
        var hasPassword = await users.HasPasswordAsync(user);
        if (hasPassword && string.IsNullOrEmpty(input.CurrentPassword))
            ModelState.AddModelError("Password.CurrentPassword", "Nhập mật khẩu hiện tại.");
        if (!ModelState.IsValid) return View("Security", await SecurityModel(user));

        var result = hasPassword
            ? await users.ChangePasswordAsync(user, input.CurrentPassword!, input.NewPassword)
            : await users.AddPasswordAsync(user, input.NewPassword);
        if (!result.Succeeded)
        {
            AddErrors(result, password: true);
            return View("Security", await SecurityModel(user));
        }
        await signIn.RefreshSignInAsync(user);
        TempData["DiscoveryMessage"] = hasPassword ? "Đã đổi mật khẩu." : "Đã tạo mật khẩu cho tài khoản.";
        return RedirectToAction(nameof(Security));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> LinkGoogle()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return NotFound();
        var model = await SecurityModel(user);
        if (!model.GoogleConfigured || model.GoogleLinked) return BadRequest();
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        var callback = Url.Action(nameof(GoogleCallback), "Account", new { area = "Tenant" })!;
        var properties = signIn.ConfigureExternalAuthenticationProperties("Google", callback, CurrentUserId);
        return Challenge(properties, "Google");
    }

    [HttpGet]
    public async Task<IActionResult> GoogleCallback()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return NotFound();
        // Identity verifies the linking user through the XSRF value placed in the external cookie.
        var info = await signIn.GetExternalLoginInfoAsync(CurrentUserId);
        if (info?.LoginProvider != "Google")
            TempData["DiscoveryError"] = "Chưa liên kết được Google. Hãy thử lại từ trang bảo mật.";
        else
        {
            var result = await users.AddLoginAsync(user, info);
            TempData[result.Succeeded ? "DiscoveryMessage" : "DiscoveryError"] = result.Succeeded
                ? "Đã liên kết tài khoản Google." : "Không thể liên kết. Tài khoản Google có thể đã được dùng cho tài khoản khác.";
        }
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
        return RedirectToAction(nameof(Security));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveGoogle()
    {
        var user = await users.GetUserAsync(User);
        if (user == null) return NotFound();
        var model = await SecurityModel(user);
        if (!model.GoogleLinked || !model.CanRemoveGoogle)
        {
            TempData["DiscoveryError"] = "Hãy tạo mật khẩu trước khi gỡ phương thức đăng nhập duy nhất.";
            return RedirectToAction(nameof(Security));
        }
        var login = (await users.GetLoginsAsync(user)).Single(l => l.LoginProvider == "Google");
        var result = await users.RemoveLoginAsync(user, login.LoginProvider, login.ProviderKey);
        if (!result.Succeeded)
        {
            AddErrors(result);
            return View("Security", await SecurityModel(user));
        }
        await signIn.RefreshSignInAsync(user);
        TempData["DiscoveryMessage"] = "Đã gỡ liên kết Google.";
        return RedirectToAction(nameof(Security));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToPage("/Account/Login", new { area = "Identity" });
    }

    private static AccountProfileModel Profile(ApplicationUser user, ProfileInput? input = null) => new()
    {
        Profile = input ?? new() { FullName = user.FullName, PhoneNumber = user.PhoneNumber },
        DisplayName = user.FullName,
        Email = user.Email,
        EmailConfirmed = user.EmailConfirmed,
        AvatarUrl = RentalPresentation.SafeMediaUrl(user.AvatarUrl)
    };

    private async Task<AccountSecurityModel> SecurityModel(ApplicationUser user)
    {
        var logins = await users.GetLoginsAsync(user);
        var hasPassword = await users.HasPasswordAsync(user);
        var policy = users.Options.Password;
        return new()
        {
            HasPassword = hasPassword,
            GoogleLinked = logins.Any(l => l.LoginProvider == "Google"),
            GoogleConfigured = (await signIn.GetExternalAuthenticationSchemesAsync()).Any(s => s.Name == "Google"),
            CanRemoveGoogle = hasPassword || logins.Any(l => l.LoginProvider != "Google"),
            RequiredLength = policy.RequiredLength,
            RequireDigit = policy.RequireDigit,
            RequireLowercase = policy.RequireLowercase,
            RequireUppercase = policy.RequireUppercase,
            RequireSymbol = policy.RequireNonAlphanumeric
        };
    }

    private void AddErrors(IdentityResult result, bool password = false)
    {
        foreach (var error in result.Errors)
        {
            var message = error.Code switch
            {
                "PasswordMismatch" => "Mật khẩu hiện tại chưa đúng.",
                "PasswordTooShort" => $"Mật khẩu cần ít nhất {users.Options.Password.RequiredLength} ký tự.",
                "PasswordRequiresDigit" => "Mật khẩu cần ít nhất một chữ số.",
                "PasswordRequiresLower" => "Mật khẩu cần ít nhất một chữ thường.",
                "PasswordRequiresUpper" => "Mật khẩu cần ít nhất một chữ hoa.",
                "PasswordRequiresNonAlphanumeric" => "Mật khẩu cần ít nhất một ký tự đặc biệt.",
                "PasswordRequiresUniqueChars" => "Mật khẩu cần thêm các ký tự khác nhau.",
                "ConcurrencyFailure" => "Thông tin vừa thay đổi. Hãy tải lại trang rồi thử lưu lại.",
                _ => "Chưa lưu được thay đổi. Hãy tải lại trang và thử lại."
            };
            var key = error.Code == "PasswordMismatch" ? "Password.CurrentPassword" : password ? "Password.NewPassword" : "";
            ModelState.AddModelError(key, message);
        }
    }

    private void DeleteAvatar(string? url)
    {
        try { TenantAvatarStorage.DeleteLocal(url, environment.WebRootPath); }
        catch (IOException ex) { logger.LogWarning(ex, "Chưa dọn được ảnh đại diện cũ."); }
        catch (UnauthorizedAccessException ex) { logger.LogWarning(ex, "Không có quyền dọn ảnh đại diện cũ."); }
    }
}
