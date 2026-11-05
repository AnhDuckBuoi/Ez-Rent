using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Services;

public static class TenantHomeReader
{
    public static async Task<TenantHomeModel> ReadAsync(AppDbContext db, string userId, DateTimeOffset now)
    {
        var vietnamNow = now.ToOffset(TimeSpan.FromHours(7)).DateTime;
        var name = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.FullName).SingleOrDefaultAsync();
        var ownAppointments = db.ViewingAppointments.AsNoTracking().Where(a => a.TenantId == userId);
        // Existing datetime-local fields store wall time. Do not shift or rewrite legacy ViewingDate values.
        var next = await ownAppointments.Where(a => a.Status == ViewingAppointmentStatus.Confirmed && a.ViewingDate >= vietnamNow)
            .OrderBy(a => a.ViewingDate).ThenBy(a => a.ViewingAppointmentId).Include(a => a.RentalPost).FirstOrDefaultAsync();
        var pending = await ownAppointments.CountAsync(a => a.Status == ViewingAppointmentStatus.Pending);
        var ownSaved = db.Favorites.AsNoTracking().Where(f => f.UserId == userId);
        var savedCount = await ownSaved.CountAsync();
        var saved = await ownSaved.OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.FavoriteId)
            .Take(2).Include(f => f.RentalPost).ThenInclude(p => p.RoomImages).ToListAsync();
        var notices = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        var unread = await notices.CountAsync(n => !n.IsRead);
        var recent = await notices.OrderByDescending(n => n.CreatedAt).ThenByDescending(n => n.NotificationId).Take(2).ToListAsync();

        var openPosts = db.RentalPosts.AsNoTracking().VisibleToTenant();
        var totalOpenPosts = await openPosts.CountAsync();
        var typeCounts = await openPosts
            .GroupBy(p => p.RoomType)
            .Select(g => new { RoomType = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RoomType, x => x.Count);
        var featuredRooms = await openPosts
            .Include(p => p.RoomImages)
            .OrderByDescending(p => p.CreatedAt)
            .ThenByDescending(p => p.RentalPostId)
            .Take(8)
            .ToListAsync();

        return new()
        {
            DisplayName = string.IsNullOrWhiteSpace(name) ? "bạn" : name.Trim(),
            Today = vietnamNow.Date,
            NextAppointment = next == null ? null : AppointmentModel.From(next),
            PendingAppointments = pending,
            SavedCount = savedCount,
            SavedRooms = saved.Select(f => TenantRentalDiscovery.Card(f.RentalPost, true)).ToList(),
            UnreadCount = unread,
            Notices = recent.Select(TenantNoticeModel.From).ToList(),
            TotalOpenPosts = totalOpenPosts,
            RoomTypeCounts = typeCounts,
            FeaturedRooms = featuredRooms
        };
    }
}
