using System.Net;
using System.Text.RegularExpressions;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Models;
using EZ_Rent.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantContractDraftTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => app.DisposeAsync();
    private const string DraftUrl = "/Tenant/Contract/Draft/1";

    private async Task<Dictionary<string, string>> Fields()
    {
        var html = await app.Client.GetStringAsync(DraftUrl);
        string Value(string field) => WebUtility.HtmlDecode(Regex.Match(html,
            "name=\"Input\\." + field + "\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
        return new()
        {
            ["Input.ExpectedRevision"] = Value("ExpectedRevision"), ["Input.ContractStamp"] = Value("ContractStamp"),
            ["Input.FullName"] = "  Người thuê minh họa  ", ["Input.PhoneNumber"] = "0901234567",
            ["Input.DateOfBirth"] = "2000-01-02", ["Input.PermanentAddress"] = "Địa chỉ thường trú minh họa",
            ["Input.DocumentKind"] = "1", ["Input.DocumentNumber"] = "DEMO-0001",
            ["Input.DocumentIssuedOn"] = "2021-03-04", ["Input.DocumentIssuedAt"] = "Nơi cấp minh họa"
        };
    }

    [Fact]
    public async Task First_get_prefills_profile_and_does_not_create_a_draft()
    {
        await app.WithDb(async db => { var user = (await db.Users.FindAsync(TenantTestApp.TenantA))!; user.FullName = "Tên từ hồ sơ"; user.PhoneNumber = "0901111222"; await db.SaveChangesAsync(); });
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync(DraftUrl));
        Assert.Contains("value=\"Tên từ hồ sơ\"", html);
        Assert.Contains("value=\"0901111222\"", html);
        Assert.Contains("Thông tin người thuê còn thiếu", html);
        await app.WithDb(async db => Assert.Empty(await db.ContractTenantDrafts.ToListAsync()));
    }

    [Fact]
    public async Task Saves_only_allowed_personal_fields_and_snapshots_the_server_agreement()
    {
        var fields = await Fields();
        fields["Input.TenantId"] = TenantTestApp.TenantB;
        fields["Input.MonthlyRent"] = "1"; fields["MonthlyRent"] = "1";
        fields["Input.LandlordName"] = "Forged landlord"; fields["Input.ContractTerms"] = "Forged terms";
        fields["Input.Revision"] = "900"; fields["Status"] = "1";
        using var response = await app.PostWithToken("/Tenant/Contract/SaveDraft/1", fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("revision=1", response.Headers.Location!.OriginalString);
        await app.WithDb(async db =>
        {
            var saved = await db.ContractTenantDrafts.SingleAsync();
            Assert.Equal(TenantTestApp.TenantA, saved.TenantId); Assert.Equal(1, saved.Revision);
            Assert.Equal("Người thuê minh họa", saved.FullName); Assert.Equal(3000000, saved.MonthlyRent);
            Assert.Equal("Landlord", saved.LandlordName); Assert.Null(saved.ContractTerms);
            Assert.Equal(new DateOnly(2000, 1, 2), saved.DateOfBirth);
            Assert.Equal("Tenant A", (await db.Users.FindAsync(TenantTestApp.TenantA))!.FullName);
            Assert.Null((await db.RentalContracts.FindAsync(1))!.TenantConfirmedAt);
            Assert.Empty(await db.ContractDocuments.ToListAsync());
        });
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync(response.Headers.Location));
        Assert.Contains("DEMO-0001", html); Assert.Contains("02/01/2000", html);
        Assert.Contains("Đã có đủ các mục thông tin người thuê", html);
    }

    [Fact]
    public async Task Partial_draft_can_be_saved_and_missing_fields_remain_explicit()
    {
        var fields = await Fields();
        foreach (var key in fields.Keys.Where(k => k is not ("Input.FullName" or "Input.ExpectedRevision" or "Input.ContractStamp")).ToList()) fields[key] = "";
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Contract/SaveDraft/1", fields)).StatusCode);
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync(DraftUrl));
        Assert.Contains("Thông tin người thuê còn thiếu", html);
        Assert.Contains("Ngày sinh", html); Assert.Contains("Số giấy tờ", html);
        Assert.Contains("CHƯA HOÀN THIỆN", html);
    }

    [Theory]
    [InlineData("Input.DocumentKind", "999")]
    [InlineData("Input.DateOfBirth", "2999-01-01")]
    [InlineData("Input.DocumentIssuedOn", "2999-01-01")]
    [InlineData("Input.DocumentIssuedOn", "1990-01-01")]
    [InlineData("Input.FullName", " ")]
    [InlineData("Input.PhoneNumber", "invalid-number")]
    [InlineData("Input.ContractStamp", "short")]
    public async Task Invalid_input_is_rejected_and_preserved_without_writing(string field, string value)
    {
        var fields = await Fields(); fields[field] = value;
        using var response = await app.PostWithToken("/Tenant/Contract/SaveDraft/1", fields);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("bm03-validation", await response.Content.ReadAsStringAsync());
        await app.WithDb(async db => Assert.Empty(await db.ContractTenantDrafts.ToListAsync()));
    }

    [Fact]
    public async Task Saving_requires_csrf_role_and_contract_ownership()
    {
        var fields = await Fields();
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsync("/Tenant/Contract/SaveDraft/1", new FormUrlEncodedContent(fields))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.PostWithToken("/Tenant/Contract/SaveDraft/2", fields)).StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role"); app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Landlord");
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.PostAsync("/Tenant/Contract/SaveDraft/1", new FormUrlEncodedContent(fields))).StatusCode);
    }

    [Fact]
    public async Task Two_tabs_cannot_overwrite_the_same_revision()
    {
        var stale = await Fields();
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Contract/SaveDraft/1", new(stale))).StatusCode);
        stale["Input.FullName"] = "Tên ở tab cũ";
        using var response = await app.PostWithToken("/Tenant/Contract/SaveDraft/1", stale);
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Tên ở tab cũ", html); Assert.Contains("Bản nháp hoặc nội dung hợp đồng vừa thay đổi", html);
        await app.WithDb(async db => Assert.Single(await db.ContractTenantDrafts.ToListAsync()));
    }

    [Fact]
    public async Task Changed_agreement_rejects_stale_form_and_preserves_previous_snapshot()
    {
        var initial = await Fields();
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Contract/SaveDraft/1", initial)).StatusCode);
        var stale = await Fields();
        await app.WithDb(async db => { var contract = (await db.RentalContracts.FindAsync(1))!; contract.MonthlyRent = 8000000; contract.Terms = "Điều khoản mới"; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Conflict, (await app.PostWithToken("/Tenant/Contract/SaveDraft/1", stale)).StatusCode);
        var fresh = await Fields();
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Contract/SaveDraft/1", fresh)).StatusCode);
        await app.WithDb(async db =>
        {
            var revisions = await db.ContractTenantDrafts.OrderBy(d => d.Revision).ToListAsync();
            Assert.Equal(2, revisions.Count); Assert.Equal(3000000, revisions[0].MonthlyRent); Assert.Null(revisions[0].ContractTerms);
            Assert.Equal(8000000, revisions[1].MonthlyRent); Assert.Equal("Điều khoản mới", revisions[1].ContractTerms);
        });
        var old = WebUtility.HtmlDecode(await app.Client.GetStringAsync(DraftUrl + "?revision=1"));
        Assert.Contains("3.000.000", old); Assert.DoesNotContain("data-contract-draft-form", old);
        Assert.Contains("Bản này chỉ để xem lại", old);
    }

    [Fact]
    public async Task Profile_changes_do_not_mutate_saved_personal_information()
    {
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Contract/SaveDraft/1", await Fields())).StatusCode);
        await app.WithDb(async db => { var user = (await db.Users.FindAsync(TenantTestApp.TenantA))!; user.FullName = "New profile name"; user.PhoneNumber = "0909999000"; await db.SaveChangesAsync(); });
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync(DraftUrl));
        Assert.Contains("Người thuê minh họa", html); Assert.DoesNotContain("New profile name", html);
        Assert.Contains("0901234567", html); Assert.DoesNotContain("0909999000", html);
    }

    [Fact]
    public async Task User_text_is_encoded_and_contract_terms_are_retained_in_the_review_appendix()
    {
        const string attack = "<script>alert(1)</script>";
        await app.WithDb(async db => { (await db.RentalContracts.FindAsync(1))!.Terms = attack; await db.SaveChangesAsync(); });
        var fields = await Fields(); fields["Input.DocumentIssuedAt"] = attack;
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Contract/SaveDraft/1", fields)).StatusCode);
        var html = await app.Client.GetStringAsync(DraftUrl);
        Assert.DoesNotContain(attack, html); Assert.Contains("&lt;script&gt;", html);
        Assert.Contains("bm03-terms-appendix", html); Assert.Contains("TRÁCH NHIỆM", WebUtility.HtmlDecode(html));
    }

    [Fact]
    public async Task Draft_save_does_not_modify_retained_document_bytes_or_acknowledgement()
    {
        var bytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6lJkAAAAASUVORK5CYII=");
        ContractDocument document = null!;
        await app.WithServices(async services =>
        {
            var documents = services.GetRequiredService<RentalDocumentService>();
            document = await documents.AppendContractAsync(1, "landlord", 0, new FormFile(new MemoryStream(bytes),0,bytes.Length,"file","document.png"));
            await documents.AcknowledgeAsync(1, document.ContractDocumentId, TenantTestApp.TenantA);
        });
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Contract/SaveDraft/1", await Fields())).StatusCode);
        Assert.Equal(bytes, await app.Client.GetByteArrayAsync($"/Tenant/Contract/Document/{document.ContractDocumentId}"));
        await app.WithDb(async db =>
        {
            var saved = await db.ContractDocuments.SingleAsync();
            Assert.NotNull(saved.TenantAcknowledgedAt); Assert.Equal(document.Sha256, saved.Sha256);
            Assert.Equal(1, (await db.RentalContracts.FindAsync(1))!.CurrentDocumentVersion);
        });
    }

    [Theory]
    [InlineData("?revision=0")]
    [InlineData("?revision=wrong")]
    [InlineData("?historyPage=0")]
    public async Task Invalid_draft_queries_fail_without_creating_data(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.GetAsync(DraftUrl + query)).StatusCode);
        await app.WithDb(async db => Assert.Empty(await db.ContractTenantDrafts.ToListAsync()));
    }

    [Fact]
    public async Task Historical_versions_are_numbered_paged_and_scoped_after_contract_reassignment()
    {
        await app.WithServices(async services =>
        {
            var service = services.GetRequiredService<TenantContractDraftService>();
            for (var revision = 0; revision < 12; revision++)
            {
                var page = (await service.GetAsync(1, TenantTestApp.TenantA))!;
                Assert.Equal(DraftSaveStatus.Saved, (await service.SaveAsync(1, TenantTestApp.TenantA, page.Input)).Status);
            }
        });
        var first = await app.Client.GetStringAsync(DraftUrl);
        var second = await app.Client.GetStringAsync(DraftUrl + "?historyPage=2");
        Assert.Equal(10, Regex.Matches(first, "data-draft-revision=").Count);
        Assert.Equal(2, Regex.Matches(second, "data-draft-revision=").Count);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync(DraftUrl + "?historyPage=2147483647")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync(DraftUrl + "?revision=999")).StatusCode);
        await app.WithDb(async db => { (await db.RentalContracts.FindAsync(1))!.TenantId = TenantTestApp.TenantB; await db.SaveChangesAsync(); });
        app.Client.DefaultRequestHeaders.Remove("X-Test-User"); app.Client.DefaultRequestHeaders.Add("X-Test-User", TenantTestApp.TenantB);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync(DraftUrl + "?revision=1")).StatusCode);
        Assert.DoesNotContain("data-draft-revision", await app.Client.GetStringAsync(DraftUrl));
    }
}
