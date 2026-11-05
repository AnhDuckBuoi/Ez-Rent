using System.Net;
using System.Text.RegularExpressions;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Models;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantShellTests
{
    [Theory]
    [InlineData("/Tenant/Dashboard", "/Tenant/Dashboard")]
    [InlineData("/Tenant/RentalPost", "/Tenant/RentalPost")]
    [InlineData("/Tenant/MyRoom", "/Tenant/MyRoom")]
    [InlineData("/Tenant/Favorite", "/Tenant/Favorite")]
    [InlineData("/Tenant/Notification", "/Tenant/Notification")]
    [InlineData("/Tenant/Support/Create", "/Tenant/Support/Create")]
    public async Task Shared_layout_keeps_centered_tenant_header_routes_and_counts_only_own_unread(string path, string activeHref)
    {
        await using var app = new TenantTestApp();
        await app.InitializeAsync();
        await app.WithDb(async db =>
        {
            db.Notifications.AddRange(
                new Notification { UserId = TenantTestApp.TenantA, Title = "own", Content = "own" },
                new Notification { UserId = TenantTestApp.TenantA, Title = "read", Content = "read", IsRead = true },
                new Notification { UserId = TenantTestApp.TenantB, Title = "private", Content = "private" });
            await db.SaveChangesAsync();
        });

        using var response = await app.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        var header = Regex.Match(html, "<header[^>]*id=\"shared-header\".*?</header>", RegexOptions.Singleline).Value;
        Assert.NotEmpty(header);
        Assert.Contains("navbar", header);
        Assert.Contains($"href=\"{activeHref}\"", header);
        Assert.Contains("aria-label=\"Phòng yêu thích\"", header);
        Assert.Contains("Thông báo, 1 chưa đọc", header);
        Assert.Contains("aria-label=\"Tài khoản của tôi\"", header);
        Assert.DoesNotContain("data-tenant-menu", header);
        Assert.DoesNotContain("tenant-desktop-header", html);
        Assert.DoesNotContain("private", header);
    }

    [Fact]
    public async Task Contextual_tenant_links_stay_accessible_from_discovery_and_account()
    {
        await using var app = new TenantTestApp();
        await app.InitializeAsync();

        using var rentalResp = await app.Client.GetAsync("/Tenant/RentalPost");
        Assert.Equal(HttpStatusCode.OK, rentalResp.StatusCode);
        var rentalHtml = WebUtility.HtmlDecode(await rentalResp.Content.ReadAsStringAsync());
        Assert.Contains("href=\"/Tenant/ViewingAppointment\"", rentalHtml);

        using var accountResp = await app.Client.GetAsync("/Tenant/Account");
        Assert.Equal(HttpStatusCode.OK, accountResp.StatusCode);
        var accountHtml = WebUtility.HtmlDecode(await accountResp.Content.ReadAsStringAsync());
        Assert.Contains("href=\"/Tenant/Support/Create\"", accountHtml);
    }

    [Fact]
    public void Numbered_pages_stay_bounded_at_last_integer_page()
    {
        var pager = new TenantPaginationModel(int.MaxValue, int.MaxValue, p => $"?Page={p}", "Pages");
        Assert.Equal(new[] { 1, int.MaxValue - 2, int.MaxValue - 1, int.MaxValue }, pager.VisiblePages);
        Assert.Equal(new[] { 1, 3, 4, 5, 6, 7, 10 },
            new TenantPaginationModel(5, 10, p => $"?Page={p}", "Pages").VisiblePages);
    }
}
