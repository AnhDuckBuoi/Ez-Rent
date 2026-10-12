using System.ComponentModel.DataAnnotations;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Models;

public class RoomImage
{
    public int RoomImageId { get; set; }

    [Required]
    [MaxLength(500)]
    public string MediaUrl { get; set; } = "";

    public MediaType MediaType { get; set; } = MediaType.Image;

    public bool IsPrimary { get; set; }

    public int DisplayOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int RentalPostId { get; set; }
    public RentalPost RentalPost { get; set; } = null!;
}