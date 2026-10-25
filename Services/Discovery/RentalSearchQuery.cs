using EZ_Rent.Models;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Services.Discovery;

public sealed record RentalSearchCriteria
{
    public string? Keyword { get; init; }
    public string? City { get; init; }
    public string? District { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public double? MinArea { get; init; }
    public double? MaxArea { get; init; }
    public RoomType? RoomType { get; init; }
    public IReadOnlyList<string> Amenities { get; init; } = [];
}

// Public discovery and Tenant share filters; the views and page sizes stay separate.
public static class RentalSearchQuery
{
    public static IQueryable<RentalPost> PublicListings(this IQueryable<RentalPost> posts) => posts
        .Where(p => p.ApprovalStatus == ApprovalStatus.Approved && p.RoomStatus == RoomStatus.Available);

    public static IQueryable<RentalPost> Filter(IQueryable<RentalPost> posts, RentalSearchCriteria input)
    {
        if (!string.IsNullOrWhiteSpace(input.Keyword)) posts = posts.Where(p => p.Title.Contains(input.Keyword) || p.Address.Contains(input.Keyword));
        if (!string.IsNullOrWhiteSpace(input.City)) posts = posts.Where(p => p.City == input.City);
        if (!string.IsNullOrWhiteSpace(input.District)) posts = posts.Where(p => p.District == input.District);
        if (input.MinPrice.HasValue) posts = posts.Where(p => p.Price >= input.MinPrice.Value);
        if (input.MaxPrice.HasValue) posts = posts.Where(p => p.Price <= input.MaxPrice.Value);
        if (input.MinArea.HasValue) posts = posts.Where(p => p.Area >= input.MinArea.Value);
        if (input.MaxArea.HasValue) posts = posts.Where(p => p.Area <= input.MaxArea.Value);
        if (input.RoomType.HasValue) posts = posts.Where(p => p.RoomType == input.RoomType.Value);
        foreach (var amenity in input.Amenities)
            posts = posts.Where(p => p.AmenitiesJson != null && p.AmenitiesJson.Contains(amenity));
        return posts;
    }

    public static IOrderedQueryable<RentalPost> Order(IQueryable<RentalPost> posts, string? sort, bool sqlite) => sort switch
    {
        "price-asc" or "price_asc" => sqlite ? posts.OrderBy(p => (double)p.Price) : posts.OrderBy(p => p.Price),
        "price-desc" or "price_desc" => sqlite ? posts.OrderByDescending(p => (double)p.Price) : posts.OrderByDescending(p => p.Price),
        "area-asc" or "area_asc" => posts.OrderBy(p => p.Area),
        "area-desc" or "area_desc" => posts.OrderByDescending(p => p.Area),
        _ => posts.OrderByDescending(p => p.PostedAt ?? p.CreatedAt)
    };
}
