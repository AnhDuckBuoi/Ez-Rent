using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Areas.Tenant.ViewModels;

public class FavoriteInputModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Vui lòng chọn phòng hợp lệ.")]
    [Display(Name = "Phòng")]
    public int RentalPostId { get; set; }
}
