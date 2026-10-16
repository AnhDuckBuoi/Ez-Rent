using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public class RepairRequestInputModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng hợp lệ.")]
    [Display(Name = "Phòng đang thuê")]
    public int RentalPostId { get; set; }

    [Required, StringLength(100)]
    [Display(Name = "Loại sự cố")]
    public string ProblemType { get; set; } = "";

    [Required, StringLength(2000)]
    [Display(Name = "Mô tả")]
    public string Description { get; set; } = "";

    [EnumDataType(typeof(RepairPriority))]
    [Display(Name = "Mức độ ưu tiên")]
    public RepairPriority Priority { get; set; } = RepairPriority.Medium;

    [Url, StringLength(500)]
    [Display(Name = "Đường dẫn ảnh (tạm thời)")]
    public string? ImageUrl { get; set; }
}
