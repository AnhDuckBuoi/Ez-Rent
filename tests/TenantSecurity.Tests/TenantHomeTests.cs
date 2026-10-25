using System.Net;
using System.Text.RegularExpressions;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantHomeTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    public async Task InitializeAsync()
    {
        await app.InitializeAsync();
        await app.WithDb(async db =>
        {
            db.Notifications.AddRange(
                new Notification { NotificationId = 1, UserId = TenantTestApp.TenantA, Title = "OWN NOTICE", Content = "Own content", Link = "/Tenant/ViewingAppointment/Details/1" },
                new Notification { NotificationId = 2, UserId = TenantTestApp.TenantB, Title = "OTHER PRIVATE NOTICE", Content = "OTHER PRIVATE CONTENT" });
            await db.SaveChangesAsync();
        });
    }
    public Task DisposeAsync() => app.DisposeAsync();
    private async Task<string> Html(string path)
    {
        using var response = await app.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Home_uses_own_records_and_the_earliest_future_confirmed_visit()
    {
        var now = new DateTimeOffset(2026, 10, 15, 0, 0, 0, TimeSpan.Zero);
        var wallTime = now.ToOffset(TimeSpan.FromHours(7)).DateTime;
        await app.WithDb(async db =>
        {
            db.ViewingAppointments.AddRange(
                new ViewingAppointment { ViewingAppointmentId = 10, TenantId = TenantTestApp.TenantA, RentalPostId = 3, ViewingDate = wallTime.AddHours(8), Status = ViewingAppointmentStatus.Confirmed },
                new ViewingAppointment { ViewingAppointmentId = 11, TenantId = TenantTestApp.TenantA, RentalPostId = 3, ViewingDate = wallTime.AddHours(7), Status = ViewingAppointmentStatus.Confirmed },
                new ViewingAppointment { ViewingAppointmentId = 12, TenantId = TenantTestApp.TenantB, RentalPostId = 3, ViewingDate = wallTime.AddHours(1), Status = ViewingAppointmentStatus.Confirmed },
                new ViewingAppointment { ViewingAppointmentId = 13, TenantId = TenantTestApp.TenantA, RentalPostId = 3, ViewingDate = wallTime.AddMinutes(-1), Status = ViewingAppointmentStatus.Confirmed },
                new ViewingAppointment { ViewingAppointmentId = 14, TenantId = TenantTestApp.TenantA, RentalPostId = 3, ViewingDate = wallTime.AddHours(2), Status = ViewingAppointmentStatus.Cancelled });
            await db.SaveChangesAsync();
            var home = await TenantHomeReader.ReadAsync(db, TenantTestApp.TenantA, now);
            Assert.Equal(11, home.NextAppointment?.Id);
            Assert.Equal("14:00", home.NextAppointment?.TimeLabel);
            Assert.Equal(1, home.PendingAppointments);
            Assert.Equal(1, home.UnreadCount);
            Assert.Equal(1, home.SavedCount);
            Assert.Single(home.Notices); Assert.Single(home.SavedRooms);
            Assert.Equal(new DateTime(2026, 10, 15), home.Today);
            Assert.Equal(wallTime.AddHours(7), (await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 11)).ViewingDate);
        });
        var html = await Html("/Tenant/Dashboard");
        Assert.Contains("Chào Tenant A!", html);
        Assert.DoesNotContain("OTHER PRIVATE", html);
        Assert.DoesNotContain(TenantTestApp.TenantB, html);
        Assert.Contains("OWN NOTICE", html);
    }

    [Fact]
    public async Task Pending_only_is_not_presented_as_confirmed_and_empty_data_is_not_fabricated()
    {
        var html = await Html("/Tenant/Dashboard");
        Assert.DoesNotContain("class=\"home-confirmed\"", html);
        Assert.Contains("chưa có lịch sắp tới được chủ nhà xác nhận", html);
        await app.WithDb(async db =>
        {
            await db.Favorites.Where(f => f.UserId == TenantTestApp.TenantA).ExecuteDeleteAsync();
            await db.Notifications.Where(n => n.UserId == TenantTestApp.TenantA).ExecuteDeleteAsync();
        });
        html = await Html("/Tenant/Dashboard");
        Assert.Contains("Bạn chưa lưu phòng nào", html); Assert.Contains("Bạn chưa có thông báo", html);
        Assert.DoesNotContain("OTHER PRIVATE", html);
    }

    [Fact]
    public async Task Home_masks_unpublished_posts_caps_saved_rooms_and_encodes_user_content()
    {
        await app.WithDb(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantA)).FullName = "<script>name()</script>";
            var post = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 3);
            post.ApprovalStatus = ApprovalStatus.Draft; post.Title = "SECRET DRAFT TITLE"; post.Address = "SECRET DRAFT ADDRESS";
            db.ViewingAppointments.Add(new ViewingAppointment { ViewingAppointmentId = 10, TenantId = TenantTestApp.TenantA, RentalPostId = 3,
                ViewingDate = DateTime.UtcNow.AddDays(10), Status = ViewingAppointmentStatus.Confirmed });
            db.Favorites.AddRange(new Favorite { UserId = TenantTestApp.TenantA, RentalPostId = 4 }, new Favorite { UserId = TenantTestApp.TenantA, RentalPostId = 5 });
            (await db.Notifications.SingleAsync(n => n.NotificationId == 1)).Title = "<img src=x onerror=alert(1)>";
            await db.SaveChangesAsync();
        });
        var response = await app.Client.GetAsync("/Tenant/Dashboard");
        var raw = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("&lt;script&gt;", raw); Assert.Contains("&lt;img", raw);
        Assert.DoesNotContain("<script>name()", raw); Assert.DoesNotContain("<img src=x", raw);
        Assert.DoesNotContain("SECRET DRAFT", raw);
        Assert.Equal(2, Regex.Matches(raw, "data-saved-room=\"").Count);
    }

    [Fact]
    public async Task Notice_pages_are_own_only_paged_stably_and_bad_page_is_recoverable()
    {
        await app.WithDb(async db =>
        {
            for (var id = 3; id <= 14; id++) db.Notifications.Add(new Notification
            { NotificationId = id, UserId = TenantTestApp.TenantA, Title = "Notice " + id, Content = "Content", CreatedAt = new DateTime(2026, 10, 15), IsRead = true });
            await db.SaveChangesAsync();
        });
        var first = await Html("/Tenant/Notification"); var second = await Html("/Tenant/Notification?page=2");
        var ids1 = Regex.Matches(first, "data-notice-id=\"(\\d+)\"").Select(m => m.Groups[1].Value).ToArray();
        var ids2 = Regex.Matches(second, "data-notice-id=\"(\\d+)\"").Select(m => m.Groups[1].Value).ToArray();
        Assert.Equal(10, ids1.Length); Assert.Equal(3, ids2.Length); Assert.Equal(13, ids1.Concat(ids2).Distinct().Count());
        Assert.DoesNotContain("OTHER PRIVATE", first + second);
        foreach (var query in new[] { "0", "abc", "-1" })
        {
            using var invalid = await app.Client.GetAsync("/Tenant/Notification?page=" + query);
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Contains("Xem tất cả thông báo", WebUtility.HtmlDecode(await invalid.Content.ReadAsStringAsync()));
        }
        Assert.Equal(ids2, Regex.Matches(await Html("/Tenant/Notification?page=2147483647"), "data-notice-id=\"(\\d+)\"").Select(m => m.Groups[1].Value));
    }

    [Fact]
    public async Task Read_operations_require_csrf_and_get_does_not_mark_read()
    {
        await Html("/Tenant/Notification/Details/1");
        await app.WithDb(async db => Assert.False((await db.Notifications.SingleAsync(n => n.NotificationId == 1)).IsRead));
        foreach (var path in new[] { "Open/1", "ReadAll" })
        {
            using var response = await app.Client.PostAsync("/Tenant/Notification/" + path, new FormUrlEncodedContent(new Dictionary<string,string>()));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal(HttpStatusCode.MethodNotAllowed, (await app.Client.GetAsync("/Tenant/Notification/" + path)).StatusCode);
        }
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/Notification/Details/2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.PostWithToken("/Tenant/Notification/Open/2", new(), "/Tenant/Dashboard")).StatusCode);
    }

    [Fact]
    public async Task Read_all_is_idempotent_owner_scoped_and_ignores_forged_redirects()
    {
        for (var repeat = 0; repeat < 2; repeat++)
        {
            var response = await app.PostWithToken("/Tenant/Notification/ReadAll", new() { ["UserId"] = TenantTestApp.TenantB, ["ReturnUrl"] = "https://example.org", ["returnHome"] = "true" }, "/Tenant/Dashboard");
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.Equal("/Tenant/Dashboard", response.Headers.Location?.OriginalString);
        }
        await app.WithDb(async db =>
        {
            Assert.True((await db.Notifications.SingleAsync(n => n.NotificationId == 1)).IsRead);
            Assert.False((await db.Notifications.SingleAsync(n => n.NotificationId == 2)).IsRead);
            Assert.Equal(2, await db.Notifications.CountAsync());
        });
        Assert.DoesNotContain("class=\"unread-count\"", await Html("/Tenant/Dashboard"));
    }

    [Fact]
    public async Task Open_uses_route_id_and_only_follows_an_owned_tenant_detail()
    {
        var response = await app.PostWithToken("/Tenant/Notification/Open/1", new() { ["id"] = "2", ["UserId"] = TenantTestApp.TenantB }, "/Tenant/Dashboard");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Tenant/ViewingAppointment/Details/1", response.Headers.Location?.OriginalString);
        await app.WithDb(async db =>
        {
            Assert.True((await db.Notifications.SingleAsync(n => n.NotificationId == 1)).IsRead);
            Assert.False((await db.Notifications.SingleAsync(n => n.NotificationId == 2)).IsRead);
            (await db.Notifications.SingleAsync(n => n.NotificationId == 1)).Link = "/Tenant/ViewingAppointment/Details/2";
            await db.SaveChangesAsync();
        });
        response = await app.PostWithToken("/Tenant/Notification/Open/1", new(), "/Tenant/Dashboard");
        Assert.Equal("/Tenant/Notification/Details/1", response.Headers.Location?.OriginalString);
        Assert.DoesNotContain("Mở nội dung liên quan", await Html("/Tenant/Notification/Details/1"));
    }

    [Theory]
    [InlineData("https://example.org/")]
    [InlineData("//example.org/")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/Landlord/Invoice/Details/1")]
    [InlineData("/Tenant/Invoice/Delete/1")]
    [InlineData("/Tenant/../Landlord/Invoice/Details/1")]
    [InlineData("/Tenant/ViewingAppointment/Details/1?returnUrl=https://example.org")]
    [InlineData("/Tenant/ViewingAppointment/Details/%32")]
    public async Task Untrusted_links_fall_back_to_the_notice_without_open_redirects(string link)
    {
        await app.WithDb(async db => { (await db.Notifications.SingleAsync(n => n.NotificationId == 1)).Link = link; await db.SaveChangesAsync(); });
        var response = await app.PostWithToken("/Tenant/Notification/Open/1", new(), "/Tenant/Dashboard");
        Assert.Equal("/Tenant/Notification/Details/1", response.Headers.Location?.OriginalString);
        var html = await Html("/Tenant/Notification/Details/1"); Assert.DoesNotContain(link, html);
    }

    [Fact]
    public async Task Saved_room_can_return_to_home_and_utc_notices_display_vietnam_time()
    {
        await app.WithDb(async db => { (await db.Notifications.SingleAsync(n => n.NotificationId == 1)).CreatedAt = new DateTime(2026,10,15,0,0,0); await db.SaveChangesAsync(); });
        Assert.Contains("15/10/2026 · 07:00", await Html("/Tenant/Dashboard"));
        Assert.Contains("Quay lại Trang chủ", await Html("/Tenant/RentalPost/Details/3?returnUrl=%2FTenant%2FDashboard"));
    }
}
