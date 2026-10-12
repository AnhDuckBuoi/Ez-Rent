namespace EZ_Rent.Models;

public class Favorite
{
    public int FavoriteId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public int RentalPostId { get; set; }
    public RentalPost RentalPost { get; set; } = null!;
}