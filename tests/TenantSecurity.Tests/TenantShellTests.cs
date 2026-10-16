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
    [InlineData("/Tenant/Favorite", "/Tenant/Favorite")]
    [InlineData("/Tenant/ViewingAppointment", "/Tenant/ViewingAppointment")]
    public async Task Shared_header_keeps_active_section_and_counts_only_own_unread(string path, string activeHref)
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
        var header = Regex.Match(html, "<header class=\"tenant-desktop-header\">[\\s\\S]*?</header>").Value;
        Assert.NotEmpty(header);
        Assert.Contains("Thông báo, 1 chưa đọc", header);
        Assert.Contains("tenant-desktop-count\">1</span>", header);
        var active = Regex.Match(header, "<a[^>]*aria-current=\"page\"[^>]*>").Value;
        Assert.Contains($"href=\"{activeHref}\"", active);
        Assert.DoesNotContain("private", header);
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
