using EZ_Rent.Models.Enums;

namespace EZ_Rent.Models
{
    public class SearchHistory
    {
        public int SearchHistoryId { get; set; }

        public string UserId { get; set; } = "";
        public ApplicationUser User { get; set; } = null!;

        public string? City { get; set; }
        public string? District { get; set; }
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public RoomType? RoomType { get; set; }

        public DateTime SearchedAt { get; set; } = DateTime.UtcNow;
    }
}