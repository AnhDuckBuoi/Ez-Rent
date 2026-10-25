using System.Net;
using EZ_Rent.Data.Seeder;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantDiscoveryIntegrationTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => app.DisposeAsync();

    [Theory]
    [InlineData("/")]
    [InlineData("/Home/Index")]
    public async Task Tenant_public_home_uses_the_single_tenant_home(string path)
    {
        using var response = await app.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Tenant/Dashboard", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Legacy_search_preserves_filters_sort_and_numbered_page_in_the_tenant_route()
    {
        using var response = await app.Client.GetAsync("/RentalPost?city=Hanoi&district=Test&minPrice=2000000&maxPrice=5000000&minArea=15&maxArea=40&roomType=0&amenities=Wifi&amenities=Bep&sortBy=price_asc&page=2");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var url = response.Headers.Location!.OriginalString;
        Assert.StartsWith("/Tenant/RentalPost", url);
        var query = QueryHelpers.ParseQuery(new Uri("http://localhost" + url).Query);
        Assert.Equal("Hanoi", query["City"]); Assert.Equal("2000000", query["MinPrice"]);
        Assert.Equal("5000000", query["MaxPrice"]); Assert.Equal("15", query["MinArea"]);
        Assert.Equal("40", query["MaxArea"]); Assert.Equal("price-asc", query["Sort"]);
        Assert.Equal("2", query["Page"]); Assert.Equal("Wifi,Bep", query["Amenities"]);
        using var details = await app.Client.GetAsync("/RentalPost/Details/3");
        Assert.Equal("/Tenant/RentalPost/Details/3", details.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Guest_home_and_discovery_remain_public()
    {
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        using var home = await app.Client.GetAsync("/Home/Index");
        using var search = await app.Client.GetAsync("/RentalPost?minPrice=0&sortBy=price_asc");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
    }

    [Fact]
    public async Task Admin_post_list_has_one_route_and_keeps_the_search_action()
    {
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
        using var response = await app.Client.GetAsync("/Admin/Post?search=Room%203");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Room 3", html); Assert.DoesNotContain("Room 5", html);
    }

    [Fact]
    public async Task Area_filter_applies_to_both_results_and_personalized_suggestions()
    {
        await app.WithDb(async db =>
        {
            var room = (await db.RentalPosts.FindAsync(3))!; room.Area = 50;
            db.RentalPosts.Add(new RentalPost { RentalPostId = 20, OwnerId = "landlord", Title = "Small room meets area", Description = "Small", Address = "Small address",
                City = "Test city", Area = 5, Price = 3000000, ApprovalStatus = ApprovalStatus.Approved, RoomStatus = RoomStatus.Available });
            await db.SaveChangesAsync();
        });
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/RentalPost?MaxArea=10"));
        Assert.Contains("Small room meets area", html); Assert.DoesNotContain("Room 3", html);
    }

    [Fact]
    public async Task Legacy_amenities_survive_search_form_and_pagination_links()
    {
        await app.WithDb(async db => { (await db.RentalPosts.FindAsync(3))!.AmenitiesJson = "[\"Wifi\",\"Bep\"]"; await db.SaveChangesAsync(); });
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/RentalPost?Amenities=Wifi,Bep"));
        Assert.Contains("Room 3", html);
        Assert.Contains("name=\"Amenities\" value=\"Wifi,Bep\"", html);
        Assert.Contains("Tiện ích: Wifi, Bep", html);
    }

    [Theory]
    [InlineData("Server=unused;Database=EZ_Rent;Integrated Security=true")]
    [InlineData("Server=unused;Integrated Security=true")]
    [InlineData("Server=unused;Database=TeamDatabase;Integrated Security=true")]
    public void Dataset_demo_rejects_group_database_names_before_opening_a_connection(string connection)
    {
        Assert.Throws<InvalidOperationException>(() => DatasetDemoSeed.DatabaseName(connection));
        Assert.Equal("EZRent_Demo_Test", DatasetDemoSeed.DatabaseName("Server=unused;Database=EZRent_Demo_Test;Integrated Security=true"));
    }
}
