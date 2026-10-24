using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Helpers;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace EZ_Rent.Areas.Landlord.ViewModels.Account;

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

/// <summary>Thông tin "Bên cho thuê" trên BM03 (ngày sinh, giấy tờ tùy thân, thường trú).</summary>
public sealed class LegalProfileInput
{
    [DataType(DataType.Date)] [Display(Name = "Ngày sinh")]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Loại giấy tờ")]
    public EZ_Rent.Models.IdentityDocumentKind? DocumentKind { get; set; }

    [RegularExpression(@"^[A-Za-z0-9]{6,20}$", ErrorMessage = "Số giấy tờ gồm 6–20 chữ hoặc số, không dấu cách.")]
    [Display(Name = "Số giấy tờ")]
    public string? DocumentNumber { get; set; }

    [DataType(DataType.Date)] [Display(Name = "Ngày cấp")]
    public DateTime? DocumentIssuedOn { get; set; }

    [StringLength(200, ErrorMessage = "Nơi cấp tối đa 200 ký tự.")] [Display(Name = "Nơi cấp")]
    public string? DocumentIssuedAt { get; set; }

    [StringLength(500, ErrorMessage = "Địa chỉ thường trú tối đa 500 ký tự.")] [Display(Name = "Địa chỉ thường trú")]
    public string? PermanentAddress { get; set; }
}

public sealed class LandlordAccountModel
{
    public LegalProfileInput Legal { get; init; } = new();
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
