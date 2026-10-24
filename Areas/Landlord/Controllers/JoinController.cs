using System.Text;
using System.Text.Encodings.Web;
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
using EZ_Rent.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC25 / R44–R46 phía chủ trọ: đăng ký tài khoản với vai trò Landlord (trang đăng ký chung hiện luôn gán Tenant).
/// Làm đúng như trang Identity/Register của nhóm: UserManager kiểm tra mật khẩu và email trùng, gửi email xác thực
/// tới trang Identity/ConfirmEmail, tuân theo cấu hình RequireConfirmedAccount/RequireConfirmedEmail trong Program.cs.
/// Địa chỉ: /Landlord/Join. Nên thêm link "Bạn là chủ trọ?" ở trang đăng ký/đăng nhập (phần của người phụ trách xác thực).
/// </summary>
[Area("Landlord")]
[AllowAnonymous] // Cố ý công khai: đây là trang đăng ký, người dùng chưa có tài khoản.
[AutoValidateAntiforgeryToken]
public sealed class JoinController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
    IEmailSender emailSender, ILogger<JoinController> logger) : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true && User.IsInRole("Landlord")) return RedirectToAction("Index", "Overview");
        return View("Index", new JoinInput());
    }

    [HttpPost]
    public async Task<IActionResult> Index(JoinInput input)
    {
        if (User.Identity?.IsAuthenticated == true) return View("Index", input);
        if (!input.Agree) ModelState.AddModelError(nameof(input.Agree), "Bạn cần cam kết thông tin đăng là thật.");
        if (!ModelState.IsValid) return View("Index", input);

        var email = input.Email.Trim();
        var user = new ApplicationUser
        {
            FullName = input.FullName.Trim(), UserName = email, Email = email,
            PhoneNumber = string.IsNullOrWhiteSpace(input.PhoneNumber) ? null : input.PhoneNumber.Trim(),
            CreatedAt = DateTime.UtcNow
        };
        var result = await users.CreateAsync(user, input.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError("", e.Code is "DuplicateUserName" or "DuplicateEmail" ? "Email này đã có tài khoản. Hãy đăng nhập hoặc dùng email khác." : e.Description);
            return View("Index", input);
        }
        var role = await users.AddToRoleAsync(user, "Landlord");
        if (!role.Succeeded)
        {
            await users.DeleteAsync(user);
            logger.LogError("Không gán được vai trò Landlord cho tài khoản mới {Email}", email);
            ModelState.AddModelError("", "Hệ thống chưa có vai trò chủ trọ. Báo quản trị viên kiểm tra dữ liệu khởi tạo.");
            return View("Index", input);
        }
        logger.LogInformation("Tạo tài khoản chủ trọ mới {UserId}", user.Id);

        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(await users.GenerateEmailConfirmationTokenAsync(user)));
        var returnUrl = Url.Action("Index", "Overview", new { area = "Landlord" }) ?? "/";
        var callback = Url.Page("/Account/ConfirmEmail", pageHandler: null,
            values: new { area = "Identity", userId = user.Id, code, returnUrl }, protocol: Request.Scheme);
        var mailSent = true;
        try
        {
            await emailSender.SendEmailAsync(email, "Xác thực tài khoản chủ trọ EZ-Rent",
                $"Chào {HtmlEncoder.Default.Encode(user.FullName)}, hãy xác thực tài khoản bằng cách <a href='{HtmlEncoder.Default.Encode(callback ?? "")}'>bấm vào đây</a>.");
        }
        catch (Exception ex)
        {
            mailSent = false;
            logger.LogError(ex, "Không gửi được email xác thực cho chủ trọ {UserId}", user.Id);
        }

        if (users.Options.SignIn.RequireConfirmedAccount)
            return RedirectToPage("/Account/RegisterConfirmation", new { area = "Identity", email, returnUrl });
        await signIn.SignInAsync(user, isPersistent: false);
        TempData[LandlordAreaController.StatusKey] = mailSent
            ? "Đã tạo tài khoản chủ trọ. Hãy mở email để xác thực, lần đăng nhập sau hệ thống sẽ yêu cầu email đã xác thực."
            : "Đã tạo tài khoản chủ trọ nhưng chưa gửi được email xác thực. Bạn có thể gửi lại ở trang tài khoản.";
        TempData[LandlordAreaController.StatusTypeKey] = mailSent ? "success" : "warning";
        return LocalRedirect(returnUrl);
    }
}
