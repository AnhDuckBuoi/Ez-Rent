using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public class RepairRequestInputModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng hợp lệ.")]
    [Display(Name = "Phòng đang thuê")]
    public int RentalPostId { get; set; }

    [Required(ErrorMessage = "Nhập loại sự cố."), StringLength(100, ErrorMessage = "Loại sự cố tối đa 100 ký tự.")]
    [Display(Name = "Loại sự cố")]
    public string ProblemType { get; set; } = "";

    [Required(ErrorMessage = "Mô tả sự cố cần xử lý."), StringLength(2000, ErrorMessage = "Mô tả tối đa 2.000 ký tự.")]
    [Display(Name = "Mô tả")]
    public string Description { get; set; } = "";

    [EnumDataType(typeof(RepairPriority), ErrorMessage = "Mức độ ưu tiên không hợp lệ.")]
    [Display(Name = "Mức độ ưu tiên")]
    public RepairPriority Priority { get; set; } = RepairPriority.Medium;

    [Url(ErrorMessage = "Nhập liên kết ảnh hợp lệ."), StringLength(500, ErrorMessage = "Liên kết ảnh tối đa 500 ký tự.")]
    [Display(Name = "Đường dẫn ảnh (tạm thời)")]
    public string? ImageUrl { get; set; }
}
