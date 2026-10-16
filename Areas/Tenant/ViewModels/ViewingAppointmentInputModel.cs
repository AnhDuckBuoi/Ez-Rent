using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public class ViewingAppointmentInputModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng hợp lệ.")]
    [Display(Name = "Phòng")]
    public int RentalPostId { get; set; }

    [Required(ErrorMessage = "Vui lòng chọn thời gian xem phòng.")]
    [Display(Name = "Thời gian xem phòng")]
    public DateTime? ViewingDate { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú của bạn")]
    public string? Note { get; set; }
}
