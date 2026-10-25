using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Services;

public static class SearchHistoryRecorder
{
    public static async Task RecordAsync(AppDbContext db, RecommendationService recommender, string userId,
        string? city, string? district, decimal? minPrice, decimal? maxPrice, RoomType? roomType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || (string.IsNullOrWhiteSpace(city) && string.IsNullOrWhiteSpace(district) &&
            !minPrice.HasValue && !maxPrice.HasValue && !roomType.HasValue)) return;
        var latest = await db.SearchHistories.AsNoTracking().Where(s => s.UserId == userId)
            .OrderByDescending(s => s.SearchedAt).ThenByDescending(s => s.SearchHistoryId).FirstOrDefaultAsync(ct);
        // Page changes, refreshes and AJAX replacements are not new search intent.
        if (latest != null && latest.SearchedAt > DateTime.UtcNow.AddMinutes(-2) && latest.City == city && latest.District == district &&
            latest.MinPrice == minPrice && latest.MaxPrice == maxPrice && latest.RoomType == roomType) return;
        var entry = new SearchHistory { UserId = userId, City = city, District = district,
            MinPrice = minPrice, MaxPrice = maxPrice, RoomType = roomType, SearchedAt = DateTime.UtcNow };
        db.SearchHistories.Add(entry);
        try { await db.SaveChangesAsync(ct); }
        catch { db.Entry(entry).State = EntityState.Detached; throw; }
        recommender.InvalidateModel();
    }
}
