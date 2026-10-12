using System.ComponentModel.DataAnnotations;

namespace EZ_Rent.Models;

public class Message
{
    public int MessageId { get; set; }

    public int ConversationId { get; set; }
    public Conversation Conversation { get; set; } = null!;

    public string SenderId { get; set; } = "";
    public ApplicationUser Sender { get; set; } = null!;

    [Required]
    [MaxLength(2000)]
    public string Content { get; set; } = "";

    public bool IsRead { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
}