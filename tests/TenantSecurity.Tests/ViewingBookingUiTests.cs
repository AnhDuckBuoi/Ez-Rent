using System.Net;
using System.Text.RegularExpressions;
using EZ_Rent.Areas.Tenant.Services;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class ViewingBookingUiTests
{
    private static async Task<string> Html(TenantTestApp app, string path)
    {
        using var response = await app.Client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Registered_source_without_open_hours_shows_empty_ui_without_inventing_slots_or_writes()
    {
        await using var app = new TenantTestApp(); await app.InitializeAsync();
        var html = await Html(app, "/Tenant/ViewingBooking/Index/3");
        Assert.Contains("Room 3", html); Assert.Contains("Address 3", html);
        Assert.Contains("Chủ nhà chưa mở lịch ngày này", html);
        Assert.DoesNotContain("name=\"SlotId\"", html);
        Assert.DoesNotContain("Lịch minh họa để thử giao diện", html);
        Assert.Contains("Yêu cầu chưa được gửi", html);
        using var post = await app.Client.PostAsync("/Tenant/ViewingBooking/Index/3", new FormUrlEncodedContent(new Dictionary<string, string> { ["SlotId"] = "anything" }));
        Assert.Equal(HttpStatusCode.MethodNotAllowed, post.StatusCode);
        await app.WithDb(db => { Assert.Equal(2, db.ViewingAppointments.Count()); return Task.CompletedTask; });
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(999)]
    public async Task Booking_page_does_not_expose_nonpublic_unavailable_or_missing_rooms(int id)
    {
        await using var app = new TenantTestApp(); await app.InitializeAsync();
        using var response = await app.Client.GetAsync($"/Tenant/ViewingBooking/Index/{id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain($"Address {id}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Preview_reader_seam_receives_logged_in_user_and_filters_by_vietnam_date_and_exact_six_hours()
    {
        var reader = new SampleReader();
        await using var app = new TenantTestApp { ViewingClock = new FixedClock(), ViewingScheduleReader = reader };
        await app.InitializeAsync();
        var html = await Html(app, "/Tenant/ViewingBooking/Index/3?date=2026-10-16&returnUrl=https://example.org");
        Assert.Equal((3, TenantTestApp.TenantA, new DateOnly(2026, 10, 16)), reader.LastRequest);
        Assert.Contains("value=\"boundary\"", html); Assert.DoesNotContain("value=\"too-soon\"", html);
        Assert.DoesNotContain("value=\"tomorrow\"", html);
        Assert.Single(Regex.Matches(html, "value=\"boundary\""));
        Assert.DoesNotContain("example.org", html);
        Assert.Contains("Lịch minh họa để thử giao diện", html);
    }

    [Theory]
    [InlineData("not-a-date")] [InlineData("2026-10-15")]
    public async Task Invalid_or_past_dates_have_a_recovery_link(string date)
    {
        await using var app = new TenantTestApp { ViewingClock = new FixedClock() }; await app.InitializeAsync();
        using var response = await app.Client.GetAsync($"/Tenant/ViewingBooking/Index/3?date={date}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Chọn lại ngày", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Room_booking_link_preserves_room_id_and_listing_filters()
    {
        await using var app = new TenantTestApp(); await app.InitializeAsync();
        var html = await Html(app, "/Tenant/RentalPost/Details/3?returnUrl=%2FTenant%2FRentalPost%3FCity%3DTest%26Page%3D2");
        Assert.Contains("/Tenant/ViewingBooking/Index/3?returnUrl=", html);
        Assert.DoesNotContain("Đặt lịch xem phòng · Sắp có", html);
        html = await Html(app, "/Tenant/ViewingBooking/Index/3?returnUrl=%2FTenant%2FRentalPost%3FCity%3DTest%26Page%3D2");
        Assert.Contains("City%3DTest%26Page%3D2", html);
        var header = Regex.Match(html, "<header[^>]*id=\"shared-header\".*?</header>", RegexOptions.Singleline).Value;
        Assert.Matches("<a[^>]*aria-current=\"page\"[^>]*href=\"/Tenant/RentalPost\"", header);
    }

    private sealed class FixedClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 10, 16, 0, 0, 0, TimeSpan.Zero);
    }
    private sealed class SampleReader : ITenantViewingScheduleReader
    {
        public (int, string, DateOnly) LastRequest { get; private set; }
        public Task<TenantViewingSchedule> ReadAsync(int postId, string tenantId, DateOnly date, CancellationToken token)
        {
            LastRequest = (postId, tenantId, date);
            var now = new FixedClock().GetUtcNow();
            return Task.FromResult(new TenantViewingSchedule(TenantScheduleState.Open,
                [new("too-soon", now.AddHours(6).AddSeconds(-1), TenantSlotState.Available),
                 new("boundary", now.AddHours(6), TenantSlotState.Available),
                 new("boundary", now.AddHours(6), TenantSlotState.Available),
                 new("tomorrow", now.AddDays(1), TenantSlotState.Available)], true));
        }
    }
}
