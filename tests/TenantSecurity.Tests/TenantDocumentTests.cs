using System.Net;
using System.Text;
using EZ_Rent.Models;
using EZ_Rent.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantDocumentTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6lJkAAAAASUVORK5CYII=");
    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => app.DisposeAsync();
    private static IFormFile Image(string name = "meter.png") => new FormFile(new MemoryStream(Png), 0, Png.Length, "file", name);

    private async Task<ContractDocument> AddDocument(int contractId = 1, int expected = 0)
    {
        ContractDocument document = null!;
        await app.WithServices(async services => document = await services.GetRequiredService<RentalDocumentService>()
            .AppendContractAsync(contractId, "landlord", expected, Image("hop-dong.png")));
        return document;
    }

    private async Task PrepareInvoice()
    {
        await app.WithDb(async db =>
        {
            var invoice = await db.Invoices.SingleAsync(i => i.InvoiceId == 1);
            invoice.ElectricUsage = 10; invoice.WaterUsage = 3;
            invoice.MeterReading = new MeterReading
            {
                RentalPostId = 1, Period = invoice.BillingPeriod, RecordedById = "landlord",
                PrevElectricIndex = 100, CurrElectricIndex = 110, ElectricUsage = 10,
                PrevWaterIndex = 20, CurrWaterIndex = 23, WaterUsage = 3
            };
            await db.SaveChangesAsync();
        });
    }

    [Fact]
    public async Task Document_bytes_are_private_and_owner_scoped_with_download_headers()
    {
        var own = await AddDocument();
        var foreign = await AddDocument(2);
        using var response = await app.Client.GetAsync($"/Tenant/Contract/Document/{own.ContractDocumentId}?download=true");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(Png, await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("attachment", response.Content.Headers.ContentDisposition?.DispositionType);
        Assert.True(response.Headers.CacheControl?.NoStore);
        Assert.Contains("nosniff", response.Headers.GetValues("X-Content-Type-Options"));
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync($"/Tenant/Contract/Document/{foreign.ContractDocumentId}")).StatusCode);
        var html = await app.Client.GetStringAsync("/Tenant/Contract/Details/1");
        Assert.DoesNotContain(own.StorageKey, html);
        Assert.DoesNotContain(app.PrivateFileRoot, html);
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync($"/Tenant/Contract/Document/{own.ContractDocumentId}")).StatusCode);
        app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Landlord");
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync($"/Tenant/Contract/Document/{own.ContractDocumentId}")).StatusCode);
    }

    [Fact]
    public async Task Acknowledgement_requires_csrf_consent_and_ownership_and_does_not_sign_or_reprice()
    {
        var document = await AddDocument();
        using var noToken = await app.Client.PostAsync("/Tenant/Contract/ConfirmDocument/1", new FormUrlEncodedContent(new Dictionary<string,string>
            { ["documentId"] = document.ContractDocumentId.ToString(), ["agreed"] = "true" }));
        Assert.Equal(HttpStatusCode.BadRequest, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Confirm(1, document.ContractDocumentId, false)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Confirm(2, document.ContractDocumentId)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Confirm(1, document.ContractDocumentId)).StatusCode);
        DateTime? first = null;
        await app.WithDb(async db =>
        {
            var saved = await db.ContractDocuments.SingleAsync();
            first = saved.TenantAcknowledgedAt;
            Assert.NotNull(first);
            Assert.Equal(TenantTestApp.TenantA, saved.TenantAcknowledgedById);
            var contract = await db.RentalContracts.SingleAsync(c => c.RentalContractId == 1);
            Assert.Null(contract.TenantConfirmedAt);
            Assert.Null(contract.LandlordConfirmedAt);
            Assert.Equal(3000000, contract.MonthlyRent);
        });
        Assert.Equal(HttpStatusCode.Redirect, (await Confirm(1, document.ContractDocumentId)).StatusCode);
        await app.WithDb(async db => Assert.Equal(first, (await db.ContractDocuments.SingleAsync()).TenantAcknowledgedAt));
    }

    [Fact]
    public async Task New_version_preserves_history_and_rejects_stale_acknowledgement()
    {
        var first = await AddDocument();
        await Confirm(1, first.ContractDocumentId);
        var second = await AddDocument(expected: 1);
        Assert.Equal(HttpStatusCode.Conflict, (await Confirm(1, first.ContractDocumentId)).StatusCode);
        Assert.Equal(Png, await app.Client.GetByteArrayAsync($"/Tenant/Contract/Document/{first.ContractDocumentId}"));
        await app.WithDb(async db =>
        {
            Assert.NotNull((await db.ContractDocuments.FindAsync(first.ContractDocumentId))!.TenantAcknowledgedAt);
            Assert.Null((await db.ContractDocuments.FindAsync(second.ContractDocumentId))!.TenantAcknowledgedAt);
            Assert.Equal(2, (await db.RentalContracts.FindAsync(1))!.CurrentDocumentVersion);
        });
    }

    [Fact]
    public async Task Upload_service_checks_landlord_and_expected_version_before_writing_files()
    {
        await app.WithServices(async services =>
        {
            var service = services.GetRequiredService<RentalDocumentService>();
            await Assert.ThrowsAsync<KeyNotFoundException>(() => service.AppendContractAsync(1, TenantTestApp.TenantA, 0, Image()));
        });
        await AddDocument();
        await app.WithServices(async services => await Assert.ThrowsAsync<RentalDocumentConflictException>(() =>
            services.GetRequiredService<RentalDocumentService>().AppendContractAsync(1, "landlord", 0, Image())));
        Assert.Single(Directory.GetFiles(app.PrivateFileRoot));
    }

    [Theory]
    [InlineData("evil.svg", "<svg></svg>")]
    [InlineData("fake.pdf", "not pdf")]
    [InlineData("fake.png", "not png")]
    public async Task Unsupported_or_mislabeled_files_are_rejected(string name, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        await app.WithServices(async services => await Assert.ThrowsAsync<InvalidDataException>(() =>
            services.GetRequiredService<RentalDocumentService>().AppendContractAsync(1, "landlord", 0,
                new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name))));
        await app.WithDb(async db => Assert.Empty(await db.ContractDocuments.ToListAsync()));
        Assert.False(Directory.Exists(app.PrivateFileRoot));
    }

    [Fact]
    public async Task Store_limits_actual_stream_size_and_rejects_path_traversal()
    {
        await app.WithServices(async services =>
        {
            var store = services.GetRequiredService<RentalPrivateFileStore>();
            Assert.Null(store.OpenRead("../../Program.cs"));
            var bytes = new byte[RentalPrivateFileStore.ImageMaxBytes + 1];
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(
                new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", "too-big.png"), false));
            await Assert.ThrowsAsync<InvalidDataException>(() => store.SaveAsync(Image("wrong.pdf"), true));
        });
    }

    [Fact]
    public async Task Invoice_capture_keeps_indices_and_photos_after_source_changes_or_deletion()
    {
        await PrepareInvoice();
        await app.WithServices(async services => await services.GetRequiredService<RentalDocumentService>()
            .CaptureInvoiceAsync(1, "landlord", Image(), Image()));
        await app.WithDb(async db =>
        {
            var reading = await db.MeterReadings.SingleAsync();
            reading.CurrElectricIndex = 999;
            reading.ElectricMeterImageUrl = "https://example.test/changed.png";
            await db.SaveChangesAsync();
        });
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/Invoice/Details/1"));
        Assert.Contains("110,00", html);
        Assert.DoesNotContain("999,00", html);
        Assert.DoesNotContain("example.test", html);
        Assert.Equal(Png, await app.Client.GetByteArrayAsync("/Tenant/Invoice/MeterImage/1?kind=electric"));
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/Invoice/MeterImage/2?kind=electric")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/Invoice/MeterImage/1?kind=../water")).StatusCode);
        await app.WithDb(async db => { db.MeterReadings.Remove(await db.MeterReadings.SingleAsync()); await db.SaveChangesAsync(); });
        Assert.Contains("110,00", WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/Invoice/Details/1")));
    }

    [Fact]
    public async Task Capture_rejects_missing_images_and_does_not_leave_first_file_when_second_is_invalid()
    {
        await PrepareInvoice();
        await app.WithServices(async services =>
        {
            var service = services.GetRequiredService<RentalDocumentService>();
            await Assert.ThrowsAsync<InvalidDataException>(() => service.CaptureInvoiceAsync(1, "landlord", Image(), null));
            await Assert.ThrowsAsync<InvalidDataException>(() => service.CaptureInvoiceAsync(1, "landlord", Image(), Image("invalid.svg")));
        });
        if (Directory.Exists(app.PrivateFileRoot)) Assert.Empty(Directory.GetFiles(app.PrivateFileRoot));
        await app.WithDb(async db => Assert.Empty(await db.InvoiceMeterEvidence.ToListAsync()));
    }

    [Fact]
    public async Task Capturing_twice_does_not_replace_existing_evidence()
    {
        await PrepareInvoice();
        await app.WithServices(async services =>
        {
            var service = services.GetRequiredService<RentalDocumentService>();
            await service.CaptureInvoiceAsync(1, "landlord", Image(), Image());
            await Assert.ThrowsAsync<RentalDocumentConflictException>(() => service.CaptureInvoiceAsync(1, "landlord", Image(), Image()));
        });
        Assert.Equal(2, Directory.GetFiles(app.PrivateFileRoot).Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Capture_rejects_wrong_room_or_period(bool wrongRoom)
    {
        await PrepareInvoice();
        await app.WithDb(async db =>
        {
            var reading = await db.MeterReadings.SingleAsync();
            if (wrongRoom) reading.RentalPostId = 2; else reading.Period = "2025-01";
            await db.SaveChangesAsync();
        });
        await app.WithServices(async services => await Assert.ThrowsAsync<InvalidDataException>(() =>
            services.GetRequiredService<RentalDocumentService>().CaptureInvoiceAsync(1, "landlord", Image(), Image())));
    }

    [Fact]
    public async Task Get_does_not_create_documents_or_capture_legacy_meter_readings()
    {
        await PrepareInvoice();
        await app.Client.GetStringAsync("/Tenant/Contract/Details/1");
        await app.Client.GetStringAsync("/Tenant/Invoice/Details/1");
        await app.WithDb(async db => { Assert.Empty(await db.ContractDocuments.ToListAsync()); Assert.Empty(await db.InvoiceMeterEvidence.ToListAsync()); });
        Assert.False(Directory.Exists(app.PrivateFileRoot));
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/Contract/Template/2")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/Tenant/Contract/Template/1")).StatusCode);
    }

    private Task<HttpResponseMessage> Confirm(int contractId, int documentId, bool agreed = true) =>
        app.PostWithToken($"/Tenant/Contract/ConfirmDocument/{contractId}", new()
        {
            ["documentId"] = documentId.ToString(), ["agreed"] = agreed ? "true" : "false",
            ["TenantId"] = TenantTestApp.TenantB, ["MonthlyRent"] = "1", ["Status"] = "1"
        });

    [Fact]
    public async Task Bm03_draft_is_owner_scoped_read_only_and_uses_agreed_units_and_values()
    {
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/Contract/Draft/1"));
        Assert.Contains("Tenant A", html);
        Assert.Contains("3.000.000", html);
        Assert.Contains("/m³", html);
        Assert.DoesNotContain("/người", html[html.IndexOf("class=\"bm03-document\"", StringComparison.Ordinal)..]);
        Assert.Contains("CHƯA HOÀN THIỆN", html);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/Contract/Draft/2")).StatusCode);
        await app.WithDb(async db => { Assert.Empty(await db.ContractDocuments.ToListAsync()); Assert.Null((await db.RentalContracts.FindAsync(1))!.TenantConfirmedAt); });
    }

    [Fact]
    public async Task Reassigning_a_legacy_contract_does_not_grant_access_to_previous_tenants_private_files()
    {
        var document = await AddDocument();
        await PrepareInvoice();
        await app.WithServices(async services => await services.GetRequiredService<RentalDocumentService>()
            .CaptureInvoiceAsync(1, "landlord", Image(), Image()));
        await app.WithDb(async db =>
        {
            (await db.RentalContracts.FindAsync(1))!.TenantId = TenantTestApp.TenantB;
            await db.SaveChangesAsync();
        });
        app.Client.DefaultRequestHeaders.Remove("X-Test-User");
        app.Client.DefaultRequestHeaders.Add("X-Test-User", TenantTestApp.TenantB);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync($"/Tenant/Contract/Document/{document.ContractDocumentId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Tenant/Invoice/MeterImage/1?kind=electric")).StatusCode);
    }

    [Fact]
    public async Task History_is_numbered_and_foreign_versions_are_excluded()
    {
        for (var version = 0; version < 12; version++) await AddDocument(expected: version);
        var foreign = await AddDocument(2);
        var first = await app.Client.GetStringAsync("/Tenant/Contract/Details/1");
        var second = await app.Client.GetStringAsync("/Tenant/Contract/Details/1?documentPage=2");
        Assert.Equal(10, System.Text.RegularExpressions.Regex.Matches(first, "data-document-id=").Count);
        Assert.Equal(2, System.Text.RegularExpressions.Regex.Matches(second, "data-document-id=").Count);
        Assert.DoesNotContain($"data-document-id=\"{foreign.ContractDocumentId}\"", first + second);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.GetAsync("/Tenant/Contract/Details/1?documentPage=0")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/Tenant/Contract/Details/1?documentPage=2147483647")).StatusCode);
    }
}
