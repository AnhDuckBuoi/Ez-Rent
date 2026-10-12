namespace EZ_Rent.Models;

public class Notification
{
    public int NotificationId { get; set; }

    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;

    public string Title { get; set; } = "";
    public string Content { get; set; } = "";
    public string? Link { get; set; }
    public string? Type { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}