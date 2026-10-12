using EZ_Rent.Models.Enums;

namespace EZ_Rent.Models;

public class Conversation
{
    public int ConversationId { get; set; }

    public string InitiatorId { get; set; } = "";
    public ApplicationUser Initiator { get; set; } = null!;

    public string RecipientId { get; set; } = "";
    public ApplicationUser Recipient { get; set; } = null!;

    public int? RentalPostId { get; set; }
    public RentalPost? RentalPost { get; set; }

    public ConversationType ConversationType { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}