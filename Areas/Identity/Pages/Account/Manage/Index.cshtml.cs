// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace EZ_Rent.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IWebHostEnvironment _env;

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IWebHostEnvironment env)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _env = env;
        }

        public string Username { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required(ErrorMessage = "Họ tên không được để trống")]
            [MaxLength(50)]
            [Display(Name = "Họ và tên")]
            public string FullName { get; set; } = "";

            [Phone(ErrorMessage = "Số điện thoại không hợp lệ")]
            [Display(Name = "Số điện thoại")]
            public string PhoneNumber { get; set; } = "";
        }

        private async Task LoadAsync(ApplicationUser user)
        {
            var userName = await _userManager.GetUserNameAsync(user);
            var phoneNumber = await _userManager.GetPhoneNumberAsync(user);
            Username = userName;

            Input = new InputModel
            {
                FullName = user.FullName,
                PhoneNumber = phoneNumber
            };
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            Username = await _userManager.GetUserNameAsync(user);

            Input = new InputModel
            {
                FullName = user.FullName,
                PhoneNumber = user.PhoneNumber
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync(IFormFile avatar)
        {
            var user = await _userManager.GetUserAsync(User);

            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            if (!ModelState.IsValid)
            {
                Username = await _userManager.GetUserNameAsync(user);
                return Page();
            }

            // ===== Update FullName + Phone =====
            user.FullName = Input.FullName?.Trim() ?? "";
            user.PhoneNumber = Input.PhoneNumber?.Trim();

            // ===== Upload Avatar =====
            if (avatar != null && avatar.Length > 0)
            {
                var allowedExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var ext = Path.GetExtension(avatar.FileName).ToLowerInvariant();

                if (!allowedExts.Contains(ext))
                {
                    ModelState.AddModelError("", "Chỉ chấp nhận file ảnh (jpg, png, gif, webp)");
                    Username = await _userManager.GetUserNameAsync(user);
                    return Page();
                }

                if (avatar.Length > 5 * 1024 * 1024)
                {
                    ModelState.AddModelError("", "Ảnh không được vượt quá 5MB");
                    Username = await _userManager.GetUserNameAsync(user);
                    return Page();
                }

                // Folder: wwwroot/uploads/avatars/
                var folder = Path.Combine(_env.WebRootPath, "uploads", "avatars");
                Directory.CreateDirectory(folder);

                var fileName = $"{user.Id}_{Guid.NewGuid()}{ext}";
                var filePath = Path.Combine(folder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }

                // Xóa avatar cũ nếu nằm trong /uploads/
                if (!string.IsNullOrEmpty(user.AvatarUrl) && user.AvatarUrl.StartsWith("/uploads/"))
                {
                    var oldPath = Path.Combine(_env.WebRootPath, user.AvatarUrl.TrimStart('/'));
                    if (System.IO.File.Exists(oldPath))
                    {
                        try { System.IO.File.Delete(oldPath); } catch { /* ignore */ }
                    }
                }

                user.AvatarUrl = $"/uploads/avatars/{fileName}";
            }

            user.UpdatedAt = DateTime.UtcNow;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                foreach (var error in updateResult.Errors)
                    ModelState.AddModelError("", error.Description);

                Username = await _userManager.GetUserNameAsync(user);
                return Page();
            }

            await _signInManager.RefreshSignInAsync(user);
            StatusMessage = "Hồ sơ của bạn đã được cập nhật.";
            return RedirectToPage();
        }
    }
}