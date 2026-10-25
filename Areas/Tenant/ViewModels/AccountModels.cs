using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public sealed class ProfileInput
{
    [Required(ErrorMessage = "Nhập họ và tên của bạn.")]
    [StringLength(50, ErrorMessage = "Họ và tên tối đa 50 ký tự.")]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; } = "";

    [Phone(ErrorMessage = "Nhập số điện thoại hợp lệ.")]
    [StringLength(20, ErrorMessage = "Số điện thoại tối đa 20 ký tự.")]
    [Display(Name = "Số điện thoại")]
    public string? PhoneNumber { get; set; }
}

public sealed class AvatarInput
{
    [Required(ErrorMessage = "Chọn ảnh bạn muốn tải lên.")]
    public IFormFile? File { get; set; }
}

public sealed class PasswordInput
{
    [DataType(DataType.Password)]
    public string? CurrentPassword { get; set; }

    [Required(ErrorMessage = "Nhập mật khẩu mới.")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu cần từ 6 đến 100 ký tự.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = "";

    [Required(ErrorMessage = "Nhập lại mật khẩu mới.")]
    [Compare(nameof(NewPassword), ErrorMessage = "Hai mật khẩu mới chưa khớp nhau.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}

public sealed class AccountProfileModel
{
    public ProfileInput Profile { get; init; } = new();
    public AvatarInput Avatar { get; init; } = new();
    public string DisplayName { get; init; } = "";
    public string? Email { get; init; }
    public bool EmailConfirmed { get; init; }
    public string? AvatarUrl { get; init; }
    public string Initials => string.Concat(DisplayName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .TakeLast(2).Select(word => System.Globalization.StringInfo.GetNextTextElement(word))).ToUpperInvariant();
}

public sealed class AccountSecurityModel
{
    public PasswordInput Password { get; init; } = new();
    public bool HasPassword { get; init; }
    public bool GoogleConfigured { get; init; }
    public bool GoogleLinked { get; init; }
    public bool CanRemoveGoogle { get; init; }
    public int RequiredLength { get; init; }
    public bool RequireDigit { get; init; }
    public bool RequireLowercase { get; init; }
    public bool RequireUppercase { get; init; }
    public bool RequireSymbol { get; init; }
}
