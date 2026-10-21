using System.Net;
using System.Text.RegularExpressions;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class AppointmentPresentationTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    public async Task InitializeAsync()
    {
        await app.InitializeAsync();
        await app.WithDb(async db =>
        {
            for (var id = 3; id <= 16; id++)
                db.ViewingAppointments.Add(new ViewingAppointment
                {
                    ViewingAppointmentId = id, TenantId = TenantTestApp.TenantA, RentalPostId = 3,
                    ViewingDate = new DateTime(2027, 1, id, 14, 0, 0),
                    Status = (ViewingAppointmentStatus)(1 + (id - 3) % 5)
                });
            (await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 2)).Note = "OTHER TENANT PRIVATE NOTE";
            await db.SaveChangesAsync();
        });
    }
    public Task DisposeAsync() => app.DisposeAsync();
    private async Task<string> Get(string path)
    {
        var response = await app.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }
    private static int[] Cards(string html) => Regex.Matches(html, "data-appointment-id=\"(\\d+)\"").Select(m => int.Parse(m.Groups[1].Value)).ToArray();

    [Fact]
    public async Task Counts_and_pagination_are_own_only_and_pages_have_no_duplicates()
    {
        var first = await Get("/Tenant/ViewingAppointment");
        var second = await Get("/Tenant/ViewingAppointment?Page=2");
        Assert.Contains("Tất cả <span>15</span>", first);
        Assert.Contains("Chờ xác nhận <span>4</span>", first);
        Assert.Equal(10, Cards(first).Length);
        Assert.Equal(5, Cards(second).Length);
        var all = Cards(first).Concat(Cards(second)).ToArray();
        Assert.Equal(15, all.Distinct().Count()); Assert.DoesNotContain(2, all);
        Assert.DoesNotContain(TenantTestApp.TenantA, first); Assert.DoesNotContain(TenantTestApp.TenantB, first);
        Assert.DoesNotContain("OTHER TENANT PRIVATE NOTE", first);
        Assert.Equal(Cards(second), Cards(await Get("/Tenant/ViewingAppointment?Page=2147483647")));
    }

    [Fact]
    public async Task Filter_applies_before_paging_and_links_preserve_status()
    {
        var html = await Get("/Tenant/ViewingAppointment?Status=3&Page=99");
        Assert.Equal(new[] { 15, 10, 5 }, Cards(html));
        await app.WithDb(async db =>
        {
            for (var id = 20; id <= 31; id++) db.ViewingAppointments.Add(new ViewingAppointment
            {
                ViewingAppointmentId = id, TenantId = TenantTestApp.TenantA, RentalPostId = 3,
                ViewingDate = new DateTime(2027, 2, id - 19, 9, 0, 0), Status = ViewingAppointmentStatus.Completed
            });
            await db.SaveChangesAsync();
        });
        var page = await Get("/Tenant/ViewingAppointment?Status=3");
        Assert.Contains("Status=3&Page=2", page);
        Assert.Equal(10, Cards(page).Length);
        Assert.Equal(5, Cards(await Get("/Tenant/ViewingAppointment?Status=3&Page=2")).Length);
    }

    [Theory]
    [InlineData("Status=999")]
    [InlineData("Status=abc")]
    [InlineData("Page=0")]
    [InlineData("Page=abc")]
    public async Task Invalid_queries_return_a_recoverable_error(string query)
    {
        var response = await app.Client.GetAsync("/Tenant/ViewingAppointment?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Không thể lọc lịch hẹn", html); Assert.Empty(Cards(html));
        Assert.Contains("Xem tất cả lịch hẹn", html);
    }

    [Fact]
    public async Task Empty_filter_is_distinct_from_no_appointments()
    {
        await app.WithDb(async db => { await db.ViewingAppointments.Where(a => a.TenantId == TenantTestApp.TenantA && a.Status == ViewingAppointmentStatus.Rejected).ExecuteDeleteAsync(); });
        Assert.Contains("Chưa có lịch ở trạng thái này", await Get("/Tenant/ViewingAppointment?Status=5"));
        await app.WithDb(async db => { await db.ViewingAppointments.Where(a => a.TenantId == TenantTestApp.TenantA).ExecuteDeleteAsync(); });
        var empty = await Get("/Tenant/ViewingAppointment");
        Assert.Contains("Bạn chưa có lịch xem phòng", empty);
        Assert.Contains("Tất cả <span>0</span>", empty);
    }

    [Fact]
    public async Task Details_encode_both_notes_and_do_not_offer_arbitrary_time_editing()
    {
        await app.WithDb(async db =>
        {
            var item = await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 1);
            item.Note = "<script>alert(1)</script>"; item.LandlordNote = "<img src=x onerror=alert(2)>";
            await db.SaveChangesAsync();
        });
        var html = await (await app.Client.GetAsync("/Tenant/ViewingAppointment/Details/1")).Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;", html); Assert.Contains("&lt;img", html);
        Assert.DoesNotContain("<script>alert(1)", html); Assert.DoesNotContain("<img src=x", html);
        Assert.DoesNotContain(TenantTestApp.TenantA, html); Assert.DoesNotContain(TenantTestApp.TenantB, html);
        Assert.DoesNotContain("/ViewingAppointment/Edit", html);
        Assert.DoesNotContain("/ViewingAppointment/Create", html);
    }

    [Fact]
    public async Task An_unpublished_post_is_masked_without_losing_the_appointment_history()
    {
        await app.WithDb(async db =>
        {
            var post = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 3);
            post.ApprovalStatus = ApprovalStatus.Draft; post.Title = "PRIVATE DRAFT"; post.Address = "PRIVATE ADDRESS";
            await db.SaveChangesAsync();
        });
        foreach (var path in new[] { "", "/Details/1", "/Delete/1" })
        {
            var html = await Get("/Tenant/ViewingAppointment" + path);
            Assert.Contains("Tin phòng không còn khả dụng", html);
            Assert.DoesNotContain("PRIVATE DRAFT", html); Assert.DoesNotContain("PRIVATE ADDRESS", html);
        }
        Assert.Contains("Private landlord note", await Get("/Tenant/ViewingAppointment/Details/1"));
    }

    [Fact]
    public async Task A_status_change_after_opening_cancel_form_returns_conflict_and_preserves_response()
    {
        await Get("/Tenant/ViewingAppointment/Delete/1");
        await app.WithDb(async db =>
        {
            var item = await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 1);
            item.Status = ViewingAppointmentStatus.Completed; item.LandlordNote = "Completed meanwhile";
            await db.SaveChangesAsync();
        });
        var response = await app.PostWithToken("/Tenant/ViewingAppointment/Delete/1", new());
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Lịch hẹn đã thay đổi", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        await app.WithDb(async db =>
        {
            var item = await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 1);
            Assert.Equal(ViewingAppointmentStatus.Completed, item.Status); Assert.Equal("Completed meanwhile", item.LandlordNote);
        });
    }

    [Fact]
    public async Task Confirmed_cancellation_keeps_history_redirects_to_details_and_repeated_cancel_conflicts()
    {
        var response = await app.PostWithToken("/Tenant/ViewingAppointment/Delete/4", new(), "/Tenant/ViewingAppointment/Delete/4");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Tenant/ViewingAppointment/Details/4", response.Headers.Location?.OriginalString);
        var html = await Get("/Tenant/ViewingAppointment/Details/4");
        Assert.Contains("Đã hủy", html); Assert.DoesNotContain("/ViewingAppointment/Delete/4", html);
        Assert.Equal(HttpStatusCode.Conflict, (await app.PostWithToken("/Tenant/ViewingAppointment/Delete/4", new())).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/ViewingAppointment/Details/2")).StatusCode);
    }
}
