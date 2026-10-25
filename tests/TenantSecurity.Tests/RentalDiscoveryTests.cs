using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class RentalDiscoveryTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    public async Task InitializeAsync()
    {
        await app.InitializeAsync();
        await app.WithDb(async db =>
        {
            var first = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 3);
            first.Price = 3000000; first.Area = 25; first.City = "Public city"; first.District = "District A";
            for (var i = 7; i <= 23; i++)
                db.RentalPosts.Add(new RentalPost
                {
                    RentalPostId = i, Title = $"Public room {i}", Description = "Room description", Address = $"Public address {i}",
                    Price = i * 100000, Area = i + 10, City = i % 2 == 0 ? "City B" : "City A", District = i % 2 == 0 ? "East" : "West",
                    RoomType = i % 2 == 0 ? RoomType.Apartment : RoomType.Room, OwnerId = "landlord",
                    ApprovalStatus = ApprovalStatus.Approved, CreatedAt = new DateTime(2026, 1, i)
                });
            var privatePost = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 4);
            privatePost.Title = "PRIVATE DRAFT TITLE"; privatePost.City = "PRIVATE CITY"; privatePost.District = "PRIVATE DISTRICT";
            await db.SaveChangesAsync();
        });
    }
    public Task DisposeAsync() => app.DisposeAsync();
    private static List<int> Cards(string html) => Regex.Matches(html, "data-post-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).ToList();
    private async Task<string> Get(string path)
    {
        var response = await app.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Discovery_only_exposes_public_available_posts_and_public_filter_choices()
    {
        var html = await Get("/Tenant/RentalPost");
        Assert.Equal(12, Cards(html).Count);
        Assert.DoesNotContain("PRIVATE CITY", html); Assert.DoesNotContain("PRIVATE DISTRICT", html); Assert.DoesNotContain("PRIVATE DRAFT TITLE", html);
        foreach (var id in new[] { 1, 2, 4, 5, 6, 999 })
        {
            var response = await app.Client.GetAsync($"/Tenant/RentalPost/Details/{id}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("PRIVATE", await response.Content.ReadAsStringAsync());
        }
        Assert.DoesNotContain(TenantTestApp.TenantA, html);
        Assert.DoesNotContain(TenantTestApp.TenantB, html);
    }

    [Theory]
    [InlineData("Keyword=Public%20room%2017", "17")]
    [InlineData("Keyword=Public%20address%207", "7")]
    [InlineData("City=City%20B&District=East&RoomType=2&MinPrice=800000&MaxPrice=1200000&MinArea=18&MaxArea=22&Sort=price-asc", "8,10,12")]
    [InlineData("City=City%20B&Sort=price-desc", "22,20,18,16,14,12,10,8")]
    [InlineData("City=City%20A&Sort=area-asc", "7,9,11,13,15,17,19,21,23")]
    [InlineData("City=City%20B&Sort=area-desc", "22,20,18,16,14,12,10,8")]
    public async Task Filters_and_sort_work_together(string query, string expected)
    {
        var html = await Get("/Tenant/RentalPost?" + query);
        Assert.Equal(expected.Split(',').Select(int.Parse), Cards(html));
    }

    [Fact]
    public async Task Paging_is_stable_preserves_filters_and_clamps_large_pages()
    {
        var first = await Get("/Tenant/RentalPost?Sort=price-asc");
        var second = await Get("/Tenant/RentalPost?Sort=price-asc&Page=2");
        Assert.Equal(12, Cards(first).Count); Assert.Equal(6, Cards(second).Count);
        Assert.Empty(Cards(first).Intersect(Cards(second)));
        Assert.Equal(Cards(second), Cards(await Get("/Tenant/RentalPost?Sort=price-asc&Page=2147483647")));
        var partialRequest = new HttpRequestMessage(HttpMethod.Get, "/Tenant/RentalPost?City=City%20A&Sort=price-asc");
        partialRequest.Headers.Add("X-Requested-With", "XMLHttpRequest");
        var partial = await (await app.Client.SendAsync(partialRequest)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("<html", partial); Assert.Contains("data-current-url", partial); Assert.Contains("City=City%20A", WebUtility.HtmlDecode(partial));
    }

    [Theory]
    [InlineData("MinPrice=-1")]
    [InlineData("MinPrice=4000000&MaxPrice=1000000")]
    [InlineData("MinArea=50&MaxArea=10")]
    [InlineData("MaxPrice=invalid")]
    [InlineData("RoomType=999")]
    [InlineData("Page=-1")]
    [InlineData("Sort=unexpected")]
    [InlineData("MinArea=NaN")]
    [InlineData("MaxArea=Infinity")]
    [InlineData("MinArea=1000000.1")]
    public async Task Invalid_filter_inputs_return_validation_without_query_results(string query)
    {
        var html = await Get("/Tenant/RentalPost?" + query);
        Assert.Empty(Cards(html)); Assert.Contains("validation-summary-errors", html);
        Assert.DoesNotContain("The value", html);
    }

    [Fact]
    public async Task Removing_a_filter_preserves_other_criteria_and_removes_dependent_district()
    {
        var html = await Get("/Tenant/RentalPost?City=City%20A&District=West&MinPrice=1000000&RoomType=1&Page=2");
        string Chip(string key)
        {
            var tag = Regex.Match(html, $"<a\\b[^>]*data-filter-key=\"{key}\"[^>]*>").Value;
            Assert.NotEmpty(tag);
            return WebUtility.HtmlDecode(Regex.Match(tag, "href=\"([^\"]+)\"").Groups[1].Value);
        }
        var withoutCity = Chip("City");
        Assert.DoesNotContain("City=", withoutCity);
        Assert.DoesNotContain("District=", withoutCity);
        Assert.Contains("MinPrice=1000000", withoutCity);
        Assert.Contains("RoomType=1", withoutCity);
        Assert.Contains("Page=1", withoutCity);
        Assert.Equal(8, Cards(await Get(withoutCity)).Count);
        var withoutPrice = Chip("MinPrice");
        Assert.DoesNotContain("MinPrice=", withoutPrice);
        Assert.Contains("City=City%20A", withoutPrice);
        Assert.Contains("District=West", withoutPrice);
        Assert.Equal(9, Cards(await Get(withoutPrice)).Count);
    }

    [Fact]
    public async Task Removing_last_favorite_on_page_two_returns_a_valid_canonical_page()
    {
        await app.WithDb(async db =>
        {
            for (var id = 7; id <= 18; id++) db.Favorites.Add(new Favorite { UserId = TenantTestApp.TenantA, RentalPostId = id });
            await db.SaveChangesAsync();
        });
        var html = await Get("/Tenant/Favorite?page=2");
        Assert.Single(Cards(html));
        var lastId = Cards(html)[0];
        await app.PostWithToken("/Tenant/Favorite/Set", new() { ["RentalPostId"] = lastId.ToString(), ["Saved"] = "false" });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/Tenant/Favorite?page=2");
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        var updated = await (await app.Client.SendAsync(request)).Content.ReadAsStringAsync();
        Assert.Equal(12, Cards(updated).Count);
        Assert.Contains("data-current-url=\"/Tenant/Favorite?page=1\"", updated);
    }

    [Fact]
    public async Task Details_encode_user_text_reject_unsafe_media_and_do_not_publish_identity_metadata()
    {
        await app.WithDb(async db =>
        {
            var room = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 7);
            room.Description = "<script>privateAttack()</script>";
            room.AmenitiesJson = "[\"Điều hòa\",\"<img src=x onerror=privateAttack()>\"]";
            room.ApprovalNote = "INTERNAL APPROVAL NOTE";
            (await db.Users.SingleAsync(u => u.Id == "landlord")).Email = "private-owner@example.test";
            db.RoomImages.Add(new RoomImage { RentalPostId = 7, MediaUrl = "javascript:privateAttack()", IsPrimary = true });
            db.RoomImages.Add(new RoomImage { RentalPostId = 7, MediaUrl = "/images/room-placeholder.svg" });
            db.Services.Add(new Service { RentalPostId = 7, Name = "Internet", UnitPrice = 100000, Unit = "tháng" });
            await db.SaveChangesAsync();
        });
        var html = await Get("/Tenant/RentalPost/Details/7?returnUrl=https://attacker.example");
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>privateAttack", html);
        Assert.DoesNotContain("javascript:privateAttack", html); Assert.DoesNotContain("INTERNAL APPROVAL NOTE", html);
        Assert.DoesNotContain("private-owner@example.test", html); Assert.DoesNotContain("attacker.example", html);
        Assert.Contains("Internet", html); Assert.Contains("room-placeholder.svg", html);
        await app.WithDb(async db => { (await db.RentalPosts.SingleAsync(p => p.RentalPostId == 7)).AmenitiesJson = "{broken"; await db.SaveChangesAsync(); });
        Assert.Contains("Thông tin tiện ích", await Get("/Tenant/RentalPost/Details/7"));
    }

    [Fact]
    public async Task Favorite_set_is_idempotent_ignores_forged_user_and_requires_csrf()
    {
        using var noToken = await app.Client.PostAsync("/Tenant/Favorite/Set", new FormUrlEncodedContent(new Dictionary<string, string> { ["RentalPostId"] = "7", ["Saved"] = "true" }));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        var fields = new Dictionary<string, string> { ["RentalPostId"] = "7", ["Saved"] = "true", ["UserId"] = TenantTestApp.TenantB, ["ReturnUrl"] = "https://attacker.example" };
        for (var i = 0; i < 2; i++)
        {
            var response = await app.PostWithToken("/Tenant/Favorite/Set", fields);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.StartsWith("/Tenant/RentalPost", response.Headers.Location!.OriginalString);
        }
        await app.WithDb(async db =>
        {
            Assert.Equal(1, await db.Favorites.CountAsync(f => f.RentalPostId == 7));
            Assert.Equal(TenantTestApp.TenantA, (await db.Favorites.SingleAsync(f => f.RentalPostId == 7)).UserId);
            db.Favorites.Add(new Favorite { UserId = TenantTestApp.TenantB, RentalPostId = 7 });
            await db.SaveChangesAsync();
        });
        fields["Saved"] = "false";
        for (var i = 0; i < 2; i++) Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Favorite/Set", fields)).StatusCode);
        await app.WithDb(async db => Assert.Equal(TenantTestApp.TenantB, (await db.Favorites.SingleAsync(f => f.RentalPostId == 7)).UserId));
        fields.Remove("Saved");
        await app.PostWithToken("/Tenant/Favorite/Set", fields);
        await app.WithDb(async db => Assert.Equal(1, await db.Favorites.CountAsync(f => f.RentalPostId == 7)));
    }

    [Fact]
    public async Task Unavailable_favorites_hide_post_data_but_can_be_removed()
    {
        await app.WithDb(async db =>
        {
            var post = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 3);
            post.ApprovalStatus = ApprovalStatus.Draft; post.Title = "PRIVATE FORMER POST"; post.Address = "PRIVATE FORMER ADDRESS";
            await db.SaveChangesAsync();
        });
        var html = await Get("/Tenant/Favorite");
        Assert.Contains("Tin không còn khả dụng", html); Assert.DoesNotContain("PRIVATE FORMER", html);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/Favorite/Details/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/Favorite/Edit/1")).StatusCode);
        Assert.DoesNotContain("PRIVATE FORMER", await Get("/Tenant/Favorite/Delete/1"));
        var fields = new Dictionary<string, string> { ["RentalPostId"] = "3", ["Saved"] = "true" };
        await app.PostWithToken("/Tenant/Favorite/Set", fields);
        fields["Saved"] = "false";await app.PostWithToken("/Tenant/Favorite/Set", fields);
        await app.WithDb(async db =>
        {
            Assert.False(await db.Favorites.AnyAsync(f => f.UserId == TenantTestApp.TenantA));
            Assert.True(await db.Favorites.AnyAsync(f => f.UserId == TenantTestApp.TenantB));
        });
    }

    [Theory]
    [InlineData("javascript:alert(1)", null)]
    [InlineData("data:image/svg+xml,test", null)]
    [InlineData("//external.example/a.jpg", null)]
    [InlineData("/\\external.example/a.jpg", null)]
    [InlineData("https://user:pass@example.test/a.jpg", null)]
    [InlineData("/images/room.svg", "/images/room.svg")]
    [InlineData("https://example.test/a.jpg", "https://example.test/a.jpg")]
    public void Media_urls_only_allow_expected_schemes_and_local_paths(string input, string? expected) => Assert.Equal(expected, RentalPresentation.SafeMediaUrl(input));
}
