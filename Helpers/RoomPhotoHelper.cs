using System;
using System.Collections.Generic;
using System.Linq;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;

namespace EZ_Rent.Helpers;

/// <summary>
/// Resolves display-only room cover photos without mutating database records.
/// Preserves real uploaded photos while replacing dummy picsum.photos placeholders
/// with local curated architectural interior images.
/// </summary>
public static class RoomPhotoHelper
{
    private static readonly string[] AllSlots =
    {
        "/images/rooms/room-1.jpg",
        "/images/rooms/room-2.jpg",
        "/images/rooms/room-3.jpg",
        "/images/rooms/room-4.jpg",
        "/images/rooms/room-5.jpg",
        "/images/rooms/room-6.jpg",
        "/images/rooms/room-7.jpg",
        "/images/rooms/room-8.jpg"
    };

    private static readonly string[] RoomPool =
    {
        "/images/rooms/room-1.jpg",
        "/images/rooms/room-2.jpg",
        "/images/rooms/room-7.jpg"
    };

    private static readonly string[] ApartmentPool =
    {
        "/images/rooms/room-3.jpg",
        "/images/rooms/room-4.jpg",
        "/images/rooms/room-8.jpg"
    };

    private static readonly string[] HousePool =
    {
        "/images/rooms/room-5.jpg",
        "/images/rooms/room-8.jpg",
        "/images/rooms/room-3.jpg"
    };

    private static readonly string[] DormPool =
    {
        "/images/rooms/room-6.jpg",
        "/images/rooms/room-1.jpg",
        "/images/rooms/room-2.jpg"
    };

    public static bool IsRealUploadedPhoto(string? rawUrl)
        => !string.IsNullOrWhiteSpace(rawUrl)
           && !rawUrl.Contains("picsum.photos", StringComparison.OrdinalIgnoreCase)
           && !rawUrl.EndsWith("room-placeholder.svg", StringComparison.OrdinalIgnoreCase);

    public static string Resolve(RentalPost post, int uniqueGridSlot = -1)
    {
        var raw = post.RoomImages?.FirstOrDefault(i => i.IsPrimary)?.MediaUrl
                  ?? post.RoomImages?.FirstOrDefault()?.MediaUrl;

        return ResolveUrl(raw, post.RentalPostId, post.RoomType, uniqueGridSlot);
    }

    public static string ResolveUrl(string? rawUrl, int rentalPostId, RoomType roomType = RoomType.Room, int uniqueGridSlot = -1)
    {
        if (IsRealUploadedPhoto(rawUrl))
        {
            return rawUrl!;
        }

        if (uniqueGridSlot >= 0)
        {
            return AllSlots[uniqueGridSlot % AllSlots.Length];
        }

        var pool = GetPool(roomType);
        var idx = Math.Abs(rentalPostId) % pool.Length;
        return pool[idx];
    }

    public static IReadOnlyList<string> ResolveGallery(IEnumerable<string?>? rawUrls, int rentalPostId, RoomType roomType = RoomType.Room)
    {
        var realPhotos = (rawUrls ?? Enumerable.Empty<string?>())
            .Where(IsRealUploadedPhoto)
            .Select(u => u!)
            .Distinct()
            .ToList();

        if (realPhotos.Count > 0)
        {
            return realPhotos;
        }

        var pool = GetPool(roomType);
        var start = Math.Abs(rentalPostId) % pool.Length;
        var gallery = new List<string>(3);
        for (int offset = 0; offset < Math.Min(3, pool.Length); offset++)
        {
            gallery.Add(pool[(start + offset) % pool.Length]);
        }
        return gallery;
    }

    private static string[] GetPool(RoomType roomType) => roomType switch
    {
        RoomType.Apartment => ApartmentPool,
        RoomType.House => HousePool,
        RoomType.Dormitory => DormPool,
        _ => RoomPool
    };
}
