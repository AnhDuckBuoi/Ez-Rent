using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EZ_Rent.Areas.Landlord.ViewModels;

public sealed class LandlordProfileInput
{
    [Required(ErrorMessage = "Họ và tên không được để trống.")]
    [StringLength(50, ErrorMessage = "Họ và tên tối đa 50 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [RegularExpression(@"^(0|\+84)[0-9]{9,10}$", ErrorMessage = "Số điện thoại dạng 0912345678 hoặc +84912345678.")]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }
}

public sealed class LandlordPasswordInput
{
    [Required(ErrorMessage = "Nhập mật khẩu hiện tại.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu hiện tại")]
    public string CurrentPassword { get; set; } = "";

    [Required(ErrorMessage = "Nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu mới dài 6–100 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu mới")]
    public string NewPassword { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [Display(Name = "Nhập lại mật khẩu mới")]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class LandlordAccountModel
{
    public LandlordProfileInput Profile { get; init; } = new();
    public LandlordPasswordInput Password { get; init; } = new();
    public string Email { get; init; } = "";
    public bool EmailConfirmed { get; init; }
    public string? AvatarUrl { get; init; }
    public DateTime CreatedAt { get; init; }
    public bool HasPassword { get; init; }
    public bool TwoFactorEnabled { get; init; }
}

/// <summary>Đăng ký tài khoản chủ trọ (R46: chọn vai trò khi đăng ký). Cùng quy tắc với trang đăng ký của Identity.</summary>
public sealed class JoinInput
{
    [Required(ErrorMessage = "Họ và tên không được để trống.")]
    [StringLength(50, ErrorMessage = "Họ và tên tối đa 50 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [Required(ErrorMessage = "Nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    [StringLength(256)]
    [Display(Name = "Email")]
    public string Email { get; set; } = "";

    [RegularExpression(@"^(0|\+84)[0-9]{9,10}$", ErrorMessage = "Số điện thoại dạng 0912345678 hoặc +84912345678.")]
    [Display(Name = "Số điện thoại (người thuê dùng để liên hệ)")]
    public string? PhoneNumber { get; set; }

    [Required(ErrorMessage = "Nhập mật khẩu.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu dài 6–100 ký tự.")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; } = "";

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Mật khẩu nhập lại không khớp.")]
    [Display(Name = "Nhập lại mật khẩu")]
    public string ConfirmPassword { get; set; } = "";

    // Kiểm tra ở server (Range cho checkbox không chạy đúng với jquery-validate phía client).
    [Display(Name = "Tôi cam kết thông tin phòng và giá đăng trên EZ-Rent là đúng thực tế.")]
    public bool Agree { get; set; }
}
