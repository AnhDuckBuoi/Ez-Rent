using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Areas.Landlord.ViewModels.Payments;

public sealed class BankInput
{
    [Required(ErrorMessage = "Chọn ngân hàng.")]
    [Display(Name = "Ngân hàng")]
    public string Bin { get; set; } = "";

    [Required(ErrorMessage = "Nhập số tài khoản.")]
    [RegularExpression(@"^[0-9]{6,19}$", ErrorMessage = "Số tài khoản gồm 6–19 chữ số, không có khoảng trắng.")]
    [Display(Name = "Số tài khoản")]
    public string AccountNumber { get; set; } = "";

    [Required(ErrorMessage = "Nhập tên chủ tài khoản.")]
    [StringLength(50, ErrorMessage = "Tên chủ tài khoản tối đa 50 ký tự.")]
    [Display(Name = "Tên chủ tài khoản")]
    public string AccountName { get; set; } = "";
}
