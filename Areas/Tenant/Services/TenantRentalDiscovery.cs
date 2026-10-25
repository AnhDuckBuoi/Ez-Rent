using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using EZ_Rent.Services.Discovery;

namespace EZ_Rent.Areas.Tenant.Services;

public static class TenantRentalDiscovery
{
    public static IQueryable<RentalPost> VisibleToTenant(this IQueryable<RentalPost> posts) => posts.PublicListings();

    public static async Task<RentalSearchPageModel> SearchAsync(AppDbContext db, RentalSearchInput input, string userId, bool valid)
    {
        var visible = db.RentalPosts.AsNoTracking().VisibleToTenant();
        var model = new RentalSearchPageModel { Filters = input };
        model.Cities = await visible.Where(p => p.City != "").Select(p => p.City).Distinct().OrderBy(x => x).ToListAsync();
        model.Districts = await visible.Where(p => p.District != "" && (input.City == null || input.City == "" || p.City == input.City))
            .Select(p => p.District).Distinct().OrderBy(x => x).ToListAsync();
        if (!valid) return model;
        var query = RentalSearchQuery.Filter(visible, input.Criteria());
        model.TotalCount = await query.CountAsync();
        input.Page = Math.Min(input.Page, model.TotalPages);
        // SQL Server sorts decimal natively. SQLite needs a numeric cast for ordering.
        var sqlite = db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite";
        var ordered = RentalSearchQuery.Order(query, input.Sort, sqlite);
        var posts = await ordered.ThenByDescending(p => p.RentalPostId).Skip((input.Page - 1) * 12).Take(12)
            .Include(p => p.RoomImages).AsSplitQuery().ToListAsync();
        var ids = posts.Select(p => p.RentalPostId).ToArray();
        var saved = await db.Favorites.Where(f => f.UserId == userId && ids.Contains(f.RentalPostId)).Select(f => f.RentalPostId).ToListAsync();
        model.Rooms = posts.Select(p => Card(p, saved.Contains(p.RentalPostId))).ToList();
        return model;
    }

    public static async Task<RentalSearchPageModel> FavoritesAsync(AppDbContext db, string userId, int page)
    {
        var query = db.Favorites.AsNoTracking().Where(f => f.UserId == userId);
        var model = new RentalSearchPageModel { IsFavorites = true, TotalCount = await query.CountAsync() };
        model.Filters.Page = Math.Clamp(page, 1, model.TotalPages);
        var favorites = await query.OrderByDescending(f => f.CreatedAt).ThenByDescending(f => f.FavoriteId)
            .Skip((model.Filters.Page - 1) * 12).Take(12).Include(f => f.RentalPost).ThenInclude(p => p.RoomImages).AsSplitQuery().ToListAsync();
        model.Rooms = favorites.Select(f => Card(f.RentalPost, true)).ToList();
        return model;
    }

    internal static RentalCardModel Card(RentalPost post, bool saved)
    {
        var available = post.ApprovalStatus == ApprovalStatus.Approved && post.RoomStatus == RoomStatus.Available;
        return new RentalCardModel
        {
            RentalPostId = post.RentalPostId, IsSaved = saved, IsAvailable = available,
            Title = available ? post.Title : "Tin không còn khả dụng",
            Address = available ? string.Join(", ", new[] { post.Address, post.District, post.City }.Where(x => !string.IsNullOrWhiteSpace(x))) : "Bạn vẫn có thể bỏ lưu tin này.",
            Price = available ? post.Price : 0, Area = available ? post.Area : 0, RoomType = available ? post.RoomType : default,
            ImageUrl = available ? post.RoomImages.Where(i => i.MediaType == MediaType.Image).OrderByDescending(i => i.IsPrimary)
                .ThenBy(i => i.DisplayOrder).ThenBy(i => i.RoomImageId).Select(i => RentalPresentation.SafeMediaUrl(i.MediaUrl)).FirstOrDefault(x => x != null) ?? RentalPresentation.Placeholder : RentalPresentation.Placeholder
        };
    }

    public static async Task<RentalDetailModel?> DetailsAsync(AppDbContext db, int? id, string userId)
    {
        var post = await db.RentalPosts.AsNoTracking().VisibleToTenant().Include(p => p.RoomImages)
            .Include(p => p.Services).Include(p => p.Owner).AsSplitQuery().FirstOrDefaultAsync(p => p.RentalPostId == id);
        if (post == null) return null;
        return new RentalDetailModel
        {
            Room = Card(post, await db.Favorites.AnyAsync(f => f.UserId == userId && f.RentalPostId == post.RentalPostId)),
            Description = post.Description,
            Images = post.RoomImages.Where(i => i.MediaType == MediaType.Image).OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder)
                .ThenBy(i => i.RoomImageId).Select(i => RentalPresentation.SafeMediaUrl(i.MediaUrl)).Where(x => x != null).Select(x => x!).Distinct().ToList(),
            Amenities = RentalPresentation.Amenities(post.AmenitiesJson), ElectricityPrice = post.ElectricityUnitPrice,
            WaterPrice = post.WaterUnitPrice, ServiceFee = post.ServiceFee,
            Services = post.Services.OrderBy(s => s.ServiceId).Select(s => new RentalServiceModel(s.Name, s.UnitPrice, s.Unit)).ToList(),
            OwnerName = post.Owner.FullName, OwnerAvatar = RentalPresentation.SafeMediaUrl(post.Owner.AvatarUrl)
        };
    }
}
