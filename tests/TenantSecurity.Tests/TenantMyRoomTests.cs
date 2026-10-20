using System.Net;
using System.Text.RegularExpressions;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantMyRoomTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => app.DisposeAsync();

    [Fact]
    public async Task Overview_uses_own_active_contract_even_when_room_is_not_public()
    {
        var html = await Html("/Tenant/MyRoom");
        Assert.Contains("Room 1", html);
        Assert.DoesNotContain("Room 2", html);
        Assert.Contains("3.000.000", html);
        Assert.Contains("data-invoice-id=\"1\"", html);
        Assert.DoesNotContain("data-invoice-id=\"2\"", html);
        Assert.Contains("data-repair-id=\"1\"", html);
        Assert.DoesNotContain("data-repair-id=\"2\"", html);
        Assert.Contains("/Tenant/RepairRequest/Create?roomId=1", html);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/MyRoom?contractId=2")).StatusCode);
    }

    [Fact]
    public async Task Overview_uses_an_image_not_a_video_or_unsafe_media_url()
    {
        await app.WithDb(async db =>
        {
            db.RoomImages.AddRange(
                new RoomImage { RentalPostId = 1, MediaUrl = "/media/private-video.mp4", MediaType = MediaType.Video, IsPrimary = true },
                new RoomImage { RentalPostId = 1, MediaUrl = "javascript:alert(1)", IsPrimary = true },
                new RoomImage { RentalPostId = 1, MediaUrl = "/images/own-room.jpg" });
            await db.SaveChangesAsync();
        });
        var html = await Html("/Tenant/MyRoom");
        Assert.Contains("src=\"/images/own-room.jpg\"", html);
        Assert.DoesNotContain("private-video.mp4", html);
        Assert.DoesNotContain("javascript:alert", html);
    }

    [Fact]
    public async Task Empty_overview_keeps_access_to_contract_history()
    {
        await app.WithDb(async db =>
        {
            (await db.RentalContracts.SingleAsync(c => c.RentalContractId == 1)).Status = ContractStatus.Expired;
            await db.SaveChangesAsync();
        });
        var html = await Html("/Tenant/MyRoom");
        Assert.Contains("Bạn chưa có phòng đang thuê", html);
        Assert.Contains("/Tenant/Contract", html);
        Assert.DoesNotContain("data-invoice-id", html);
        Assert.DoesNotContain("3.000.000", html);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/MyRoom?contractId=1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/Tenant/Contract/Details/1")).StatusCode);
    }

    [Fact]
    public async Task Multiple_rooms_are_selected_by_owned_contract_not_by_submitted_tenant()
    {
        await AddSecondOwnContract();
        var html = await Html("/Tenant/MyRoom?contractId=3&TenantId=" + TenantTestApp.TenantB);
        Assert.Contains("name=\"contractId\"", html);
        Assert.Contains("id=\"room-heading\">Room 6", html);
        Assert.Contains("8.000.000", html);
        Assert.DoesNotContain("data-invoice-id=\"1\"", html);
        Assert.DoesNotContain("data-repair-id=\"1\"", html);
        Assert.Contains("/Tenant/Invoice?ContractId=3", html);
    }

    [Fact]
    public async Task Outstanding_summary_excludes_paid_cancelled_other_contracts_and_other_tenants()
    {
        await AddSecondOwnContract();
        await app.WithDb(async db =>
        {
            var monthOffset = 0;
            foreach (var (status, amount) in new[] { (PaymentStatus.Paid, 100m), (PaymentStatus.Cancelled, 200m), (PaymentStatus.AwaitingConfirmation, 50m), (PaymentStatus.Overdue, 60m) })
                db.Invoices.Add(new Invoice { RentalContractId = 1, BillingPeriod = new DateTime(2025, 1, 1).AddMonths(monthOffset++).ToString("yyyy-MM"), TotalAmount = amount, PaymentStatus = status });
            db.Invoices.Add(new Invoice { RentalContractId = 3, BillingPeriod = "2026-10", TotalAmount = 9000000 });
            (await db.Invoices.SingleAsync(i => i.InvoiceId == 2)).TotalAmount = 8000000;
            await db.SaveChangesAsync();
        });
        var html = await Html("/Tenant/MyRoom?contractId=1");
        Assert.Contains("3.500.110", html);
        Assert.DoesNotContain("9.000.000", html);
        Assert.DoesNotContain("8.000.000", html);
    }

    [Theory]
    [InlineData("/Tenant/MyRoom?contractId=0")]
    [InlineData("/Tenant/MyRoom?contractId=not-a-number")]
    [InlineData("/Tenant/Contract?Status=999")]
    [InlineData("/Tenant/Invoice?Status=wrong")]
    [InlineData("/Tenant/RepairRequest?Status=999")]
    [InlineData("/Tenant/Invoice?Page=0")]
    [InlineData("/Tenant/RepairRequest?Page=-1")]
    [InlineData("/Tenant/Invoice?ContractId=-1")]
    public async Task Invalid_filters_fail_without_writing(string url)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.GetAsync(url)).StatusCode);
        await app.WithDb(async db => Assert.Equal(2, await db.Invoices.CountAsync()));
    }

    [Theory]
    [InlineData("/Tenant/Invoice?ContractId=2")]
    [InlineData("/Tenant/RepairRequest?RoomId=2")]
    [InlineData("/Tenant/RepairRequest/Create?roomId=2")]
    public async Task Foreign_filter_and_preselected_room_do_not_disclose_records(string url)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync(url)).StatusCode);
    }

    [Theory]
    [InlineData("Contract", "data-contract-id")]
    [InlineData("Invoice", "data-invoice-id")]
    [InlineData("RepairRequest", "data-repair-id")]
    public async Task Lists_have_stable_numbered_pages_and_clamp_large_page_numbers(string controller, string marker)
    {
        await app.WithDb(async db =>
        {
            for (var i = 0; i < 12; i++)
            {
                db.RentalContracts.Add(new RentalContract { TenantId = TenantTestApp.TenantA, RentalPostId = 1, LandlordId = "landlord", Status = ContractStatus.Expired });
                db.Invoices.Add(new Invoice { RentalContractId = 1, BillingPeriod = new DateTime(2024, 1, 1).AddMonths(i).ToString("yyyy-MM") });
                db.RepairRequests.Add(new RepairRequest { TenantId = TenantTestApp.TenantA, RentalPostId = 1, ProblemType = "Điện", Description = "Sự cố" });
            }
            await db.SaveChangesAsync();
        });
        var first = await Html($"/Tenant/{controller}?Page=1");
        var second = await Html($"/Tenant/{controller}?Page=2");
        var ids1 = RowIds(first, marker);
        var ids2 = RowIds(second, marker);
        Assert.Equal(10, ids1.Count);
        Assert.Equal(3, ids2.Count);
        Assert.Empty(ids1.Intersect(ids2));
        Assert.DoesNotContain("2", ids1.Concat(ids2));
        Assert.Contains("aria-label=\"Trang 2\"", first);
        Assert.Equal(ids2, RowIds(await Html($"/Tenant/{controller}?Page=2147483647"), marker));
    }

    [Fact]
    public async Task Invoice_and_repair_filters_remain_owner_scoped_and_paging_retains_context()
    {
        await app.WithDb(async db =>
        {
            for (var i = 0; i < 11; i++)
                db.Invoices.Add(new Invoice { RentalContractId = 1, BillingPeriod = new DateTime(2024, 1, 1).AddMonths(i).ToString("yyyy-MM"), PaymentStatus = PaymentStatus.Paid });
            await db.SaveChangesAsync();
        });
        var filtered = await Html("/Tenant/Invoice?ContractId=1&Status=3");
        Assert.DoesNotContain("data-invoice-id=\"1\"", filtered);
        Assert.Contains("Status=3", filtered);
        Assert.Contains("ContractId=1", filtered);
        Assert.DoesNotContain("data-invoice-id=\"2\"", filtered);
        Assert.DoesNotContain("data-repair-id", await Html("/Tenant/RepairRequest?RoomId=1&Status=3"));
    }

    [Fact]
    public async Task Invoice_detail_shows_saved_amounts_and_only_matching_room_period_meter()
    {
        await app.WithDb(async db =>
        {
            var invoice = await db.Invoices.SingleAsync(i => i.InvoiceId == 1);
            invoice.RoomRentAmount = 3000000; invoice.AdjustmentAmount = -10000;
            invoice.MeterReading = new MeterReading { RentalPostId = 2, Period = invoice.BillingPeriod, RecordedById = "landlord", CurrElectricIndex = 987654 };
            await db.SaveChangesAsync();
        });
        var html = await Html("/Tenant/Invoice/Details/1");
        Assert.DoesNotContain("987.654", html);
        Assert.Contains("Chưa có chỉ số điện nước phù hợp", html);
        Assert.Contains("3.500.000", html);
        Assert.Contains("-10.000", html);
        await app.WithDb(async db =>
        {
            var reading = await db.MeterReadings.SingleAsync();
            reading.RentalPostId = 1; reading.Period = "2026-09";
            await db.SaveChangesAsync();
        });
        Assert.DoesNotContain("987.654", await Html("/Tenant/Invoice/Details/1"));
        await app.WithDb(async db => { (await db.MeterReadings.SingleAsync()).Period = "2026-10"; await db.SaveChangesAsync(); });
        Assert.Contains("987.654", await Html("/Tenant/Invoice/Details/1"));
    }

    [Fact]
    public async Task Repair_form_handles_missing_fields_empty_rooms_and_rejects_non_http_images()
    {
        using var missing = await app.PostWithToken("/Tenant/RepairRequest/Create", new() { ["RentalPostId"] = "1" });
        Assert.Equal(HttpStatusCode.OK, missing.StatusCode);
        Assert.Contains("field-validation-error", await missing.Content.ReadAsStringAsync());
        using var badImage = await app.PostWithToken("/Tenant/RepairRequest/Create", new()
        { ["RentalPostId"] = "1", ["ProblemType"] = "Điện", ["Description"] = "Hỏng đèn", ["ImageUrl"] = "ftp://example.test/photo.jpg" });
        Assert.Equal(HttpStatusCode.OK, badImage.StatusCode);
        await app.WithDb(async db =>
        {
            Assert.Equal(2, await db.RepairRequests.CountAsync());
            (await db.RentalContracts.SingleAsync(c => c.RentalContractId == 1)).Status = ContractStatus.Expired;
            await db.SaveChangesAsync();
        });
        var noRoom = await Html("/Tenant/RepairRequest/Create");
        Assert.Contains("Chưa có phòng đủ điều kiện", noRoom);
        Assert.DoesNotContain("name=\"Description\"", noRoom);
    }

    [Fact]
    public async Task Repair_conflict_has_recovery_and_unsafe_text_is_encoded()
    {
        await app.WithDb(async db =>
        {
            var request = await db.RepairRequests.SingleAsync(r => r.RepairRequestId == 1);
            request.Description = "<script>alert('bad')</script>";
            request.Status = RepairRequestStatus.InProgress;
            request.ImageUrl = "javascript:alert(1)";
            await db.SaveChangesAsync();
        });
        var html = await (await app.Client.GetAsync("/Tenant/RepairRequest/Details/1")).Content.ReadAsStringAsync();
        Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("javascript:alert", html);
        Assert.DoesNotContain("/Tenant/RepairRequest/Edit/1", html);
        using var conflict = await app.Client.GetAsync("/Tenant/RepairRequest/Delete/1");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Contains("/Tenant/RepairRequest", await conflict.Content.ReadAsStringAsync());
    }

    private async Task AddSecondOwnContract() => await app.WithDb(async db =>
    {
        db.RentalContracts.Add(new RentalContract
        {
            RentalContractId = 3, RentalPostId = 6, TenantId = TenantTestApp.TenantA,
            LandlordId = "landlord", Status = ContractStatus.Active, MonthlyRent = 8000000
        });
        await db.SaveChangesAsync();
    });

    private async Task<string> Html(string path)
    {
        using var response = await app.Client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    private static List<string> RowIds(string html, string marker) => Regex.Matches(html, marker + "=\"(\\d+)\"")
        .Select(match => match.Groups[1].Value).ToList();
}
