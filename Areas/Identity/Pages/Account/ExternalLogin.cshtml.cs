// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace EZ_Rent.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ExternalLoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly IEmailSender _emailSender;
        private readonly ILogger<ExternalLoginModel> _logger;

        public ExternalLoginModel(
            SignInManager<ApplicationUser> signInManager,
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            ILogger<ExternalLoginModel> logger,
            IEmailSender emailSender)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _logger = logger;
            _emailSender = emailSender;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ProviderDisplayName { get; set; }

        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Display(Name = "Vai trò")]
            public string Role { get; set; } = RegisterModel.RoleTenant;

            [Required(ErrorMessage = "Email không được để trống")]
            [EmailAddress(ErrorMessage = "Địa chỉ email không hợp lệ")]
            public string Email { get; set; }
        }

        public IActionResult OnGet() => RedirectToPage("./Login");

        public IActionResult OnPost(string provider, string returnUrl = null)
        {
            // Request a redirect to the external login provider.
            var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
            var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
            return new ChallengeResult(provider, properties);
        }

        public async Task<IActionResult> OnGetCallbackAsync(string returnUrl = null, string remoteError = null)
        {
            returnUrl = returnUrl ?? Url.Content("~/");
            if (remoteError != null)
            {
                ErrorMessage = $"Error from external provider: {remoteError}";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            // Sign in the user with this external login provider if the user already has a login.
            var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
            if (result.Succeeded)
            {
                // ============================================================
                // CẬP NHẬT FullName + AvatarUrl NẾU CÒN TRỐNG
                // ============================================================
                var existingUser = await _userManager.FindByLoginAsync(info.LoginProvider, info.ProviderKey);
                if (existingUser != null)
                {
                    bool needUpdate = false;

                    if (string.IsNullOrWhiteSpace(existingUser.FullName))
                    {
                        var googleName = GetGoogleName(info.Principal);
                        if (!string.IsNullOrWhiteSpace(googleName))
                        {
                            existingUser.FullName = googleName;
                            needUpdate = true;
                            _logger.LogInformation("Đã cập nhật FullName = {Name} cho user {Email}",
                                googleName, existingUser.Email);
                        }
                    }

                    if (string.IsNullOrWhiteSpace(existingUser.AvatarUrl))
                    {
                        var picture = info.Principal.FindFirstValue("urn:google:picture")
                                   ?? info.Principal.FindFirstValue("picture");
                        if (!string.IsNullOrWhiteSpace(picture))
                        {
                            existingUser.AvatarUrl = picture;
                            needUpdate = true;
                        }
                    }

                    if (needUpdate)
                        await _userManager.UpdateAsync(existingUser);
                }

                _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity.Name, info.LoginProvider);
                return LocalRedirect(returnUrl);
            }
            if (result.IsLockedOut)
            {
                return RedirectToPage("./Lockout");
            }
            else
            {
                // If the user does not have an account, then ask the user to create an account.
                ReturnUrl = returnUrl;
                ProviderDisplayName = info.ProviderDisplayName;
                Input = new InputModel
                {
                    Role = RegisterModel.RoleTenant,
                    Email = info.Principal.HasClaim(c => c.Type == ClaimTypes.Email)
                        ? info.Principal.FindFirstValue(ClaimTypes.Email)
                        : null
                };
                return Page();
            }
        }

        public async Task<IActionResult> OnPostConfirmationAsync(string returnUrl = null)
        {
            var hasExplicitReturnUrl = !string.IsNullOrWhiteSpace(returnUrl)
                && returnUrl != "/"
                && returnUrl != "~/";
            returnUrl = returnUrl ?? Url.Content("~/");
            // Get the information about the user from the external login provider
            var info = await _signInManager.GetExternalLoginInfoAsync();
            if (info == null)
            {
                ErrorMessage = "Error loading external login information during confirmation.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }

            Input ??= new InputModel();
            if (!RegisterModel.TryNormalizeSelfRegisterRole(Input.Role, out var selectedRole))
            {
                ModelState.AddModelError($"{nameof(Input)}.{nameof(Input.Role)}", "Vai trò đăng ký không hợp lệ.");
            }

            if (ModelState.IsValid)
            {
                var user = CreateUser();

                // ============================================================
                // GÁN FullName + AvatarUrl + Status TỪ GOOGLE
                // ============================================================
                var googleName = GetGoogleName(info.Principal);
                if (string.IsNullOrWhiteSpace(googleName))
                    googleName = Input.Email.Split('@')[0];

                var picture = info.Principal.FindFirstValue("urn:google:picture")
                           ?? info.Principal.FindFirstValue("picture");

                user.FullName = googleName;
                user.AvatarUrl = picture;
                user.Status = EZ_Rent.Models.Enums.UserStatus.Active;
                user.CreatedAt = DateTime.UtcNow;

                _logger.LogInformation("Tạo user mới từ Google: Email = {Email}, FullName = {FullName}, Role = {Role}",
                    Input.Email, googleName, selectedRole);

                await _userStore.SetUserNameAsync(user, Input.Email, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);

                var result = await _userManager.CreateAsync(user);
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, selectedRole);

                    result = await _userManager.AddLoginAsync(user, info);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                        var targetRedirect = (selectedRole == RegisterModel.RoleLandlord && !hasExplicitReturnUrl)
                            ? Url.Content("~/Landlord/Overview")
                            : returnUrl;

                        var userId = await _userManager.GetUserIdAsync(user);
                        var code = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                        code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));
                        var callbackUrl = Url.Page(
                            "/Account/ConfirmEmail",
                            pageHandler: null,
                            values: new { area = "Identity", userId = userId, code = code },
                            protocol: Request.Scheme);

                        await _emailSender.SendEmailAsync(Input.Email, "Confirm your email",
                            $"Please confirm your account by <a href='{HtmlEncoder.Default.Encode(callbackUrl)}'>clicking here</a>.");

                        // If account confirmation is required, we need to show the link if we don't have a real email sender
                        if (_userManager.Options.SignIn.RequireConfirmedAccount)
                        {
                            return RedirectToPage("./RegisterConfirmation", new { Email = Input.Email });
                        }

                        await _signInManager.SignInAsync(user, isPersistent: false, info.LoginProvider);
                        return LocalRedirect(targetRedirect);
                    }
                }
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            ProviderDisplayName = info.ProviderDisplayName;
            ReturnUrl = returnUrl;
            return Page();
        }

        // ============================================================
        // HÀM LẤY TÊN TỪ GOOGLE (4 cấp fallback)
        // ============================================================
        private static string GetGoogleName(ClaimsPrincipal principal)
        {
            var name = principal.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(name))
                name = principal.FindFirstValue("name");

            if (string.IsNullOrWhiteSpace(name))
            {
                var given = principal.FindFirstValue(ClaimTypes.GivenName);
                var surname = principal.FindFirstValue(ClaimTypes.Surname);

                if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(surname))
                    name = $"{surname} {given}".Trim();
            }

            if (string.IsNullOrWhiteSpace(name))
                name = principal.FindFirstValue(ClaimTypes.Email);

            return name ?? "";
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'. " +
                    $"Ensure that '{nameof(ApplicationUser)}' is not an abstract class and has a parameterless constructor, or alternatively " +
                    $"override the external login page in /Areas/Identity/Pages/Account/ExternalLogin.cshtml");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }
}
