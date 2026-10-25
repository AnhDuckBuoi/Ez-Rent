using System.Net;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantIntegrationTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => app.DisposeAsync();

    [Fact]
    public async Task Search_reuses_history_and_does_not_duplicate_on_refresh_paging_or_ajax()
    {
        const string url = "/Tenant/RentalPost?City=Test%20city&MinPrice=0&MaxPrice=5000000";
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync(url)).StatusCode);
        using var ajax = new HttpRequestMessage(HttpMethod.Get,url); ajax.Headers.Add("X-Requested-With","XMLHttpRequest");
        Assert.Equal(HttpStatusCode.OK, (await app.Client.SendAsync(ajax)).StatusCode);
        await app.WithDb(async db => { var history = await db.SearchHistories.SingleAsync(); Assert.Equal(TenantTestApp.TenantA, history.UserId); Assert.Equal(5000000, history.MaxPrice); });
    }

    [Fact]
    public async Task Invalid_search_never_records_history_and_suggestions_never_show_private_posts()
    {
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync("/Tenant/RentalPost?MinPrice=-1")).StatusCode);
        await app.WithDb(async db => Assert.Equal(0,await db.SearchHistories.CountAsync()));
        var html=WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/RentalPost"));
        Assert.Contains("Gợi ý cho bạn",html); Assert.DoesNotContain("Room 5",html); Assert.DoesNotContain("Room 4",html);
    }

    [Fact]
    public async Task Public_toggle_compatibility_still_rejects_nonpublic_posts()
    {
        using var response=await app.PostWithToken("/Tenant/Favorite/Toggle",new() { ["rentalPostId"]="5" });
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
        await app.WithDb(async db=>Assert.False(await db.Favorites.AnyAsync(f=>f.UserId==TenantTestApp.TenantA&&f.RentalPostId==5)));
    }

    [Fact]
    public async Task Landlord_can_read_and_reply_to_the_same_conversation_but_get_never_marks_read()
    {
        await app.WithDb(async db=>{
            db.Conversations.Add(new Conversation { ConversationId=90,InitiatorId=TenantTestApp.TenantA,RecipientId="landlord",RentalPostId=3,ConversationType=ConversationType.TenantLandlord });
            db.Messages.Add(new Message { ConversationId=90,SenderId=TenantTestApp.TenantA,Content="Tin của người thuê" }); await db.SaveChangesAsync();
        });
        app.Client.DefaultRequestHeaders.Remove("X-Test-User");app.Client.DefaultRequestHeaders.Add("X-Test-User","landlord");
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");app.Client.DefaultRequestHeaders.Add("X-Test-Role","Landlord");
        var html=WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Landlord/Messages?c=90"));Assert.Contains("Tin của người thuê",html);
        await app.WithDb(async db=>Assert.False((await db.Messages.SingleAsync()).IsRead));
        using var send=await app.PostWithToken("/Landlord/Messages/Send/90",new() { ["Content"]="Phản hồi từ chủ nhà" },"/Landlord/Messages?c=90");
        Assert.Equal(HttpStatusCode.Redirect,send.StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-User");app.Client.DefaultRequestHeaders.Add("X-Test-User",TenantTestApp.TenantA);
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");app.Client.DefaultRequestHeaders.Add("X-Test-Role","Tenant");
        Assert.Contains("Phản hồi từ chủ nhà",WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/Dashboard/Messages?ConversationId=90")));
        await app.WithDb(async db=>Assert.Equal(1,await db.Conversations.CountAsync()));
    }

    [Fact]
    public async Task Landlord_cannot_send_to_other_members_or_without_csrf()
    {
        await app.WithDb(async db=>{
            db.Users.Add(new ApplicationUser { Id="other-landlord",UserName="other-landlord",FullName="Other landlord" });
            db.Conversations.Add(new Conversation { ConversationId=90,InitiatorId=TenantTestApp.TenantA,RecipientId="other-landlord",ConversationType=ConversationType.TenantLandlord });await db.SaveChangesAsync();
        });
        app.Client.DefaultRequestHeaders.Remove("X-Test-User");app.Client.DefaultRequestHeaders.Add("X-Test-User","landlord");
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");app.Client.DefaultRequestHeaders.Add("X-Test-Role","Landlord");
        Assert.Equal(HttpStatusCode.NotFound,(await app.Client.GetAsync("/Landlord/Messages?c=90")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,(await app.Client.PostAsync("/Landlord/Messages/Send/90",new FormUrlEncodedContent(new Dictionary<string,string>{["Content"]="Forged"}))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await app.PostWithToken("/Landlord/Messages/Send/90",new(){["Content"]="Forged"},"/Landlord/Messages")).StatusCode);
        await app.WithDb(async db=>Assert.Equal(0,await db.Messages.CountAsync()));
    }

    [Fact]
    public async Task Seen_receipts_require_the_recipient_read_post_and_work_in_both_directions()
    {
        await app.WithDb(async db =>
        {
            db.Conversations.Add(new Conversation { ConversationId = 90, InitiatorId = TenantTestApp.TenantA,
                RecipientId = "landlord", RentalPostId = 3, ConversationType = ConversationType.TenantLandlord });
            db.Messages.AddRange(
                new Message { MessageId = 901, ConversationId = 90, SenderId = TenantTestApp.TenantA, Content = "Tin của người thuê" },
                new Message { MessageId = 902, ConversationId = 90, SenderId = "landlord", Content = "Tin của chủ nhà" });
            await db.SaveChangesAsync();
        });
        var tenantPage = await app.Client.GetStringAsync("/Tenant/Dashboard/Messages?ConversationId=90");
        Assert.DoesNotContain("Đã xem", WebUtility.HtmlDecode(tenantPage));
        app.Client.DefaultRequestHeaders.Remove("X-Test-User"); app.Client.DefaultRequestHeaders.Add("X-Test-User", "landlord");
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role"); app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Landlord");
        var landlordPage = await app.Client.GetStringAsync("/Landlord/Messages?c=90");
        Assert.DoesNotContain("Đã xem", WebUtility.HtmlDecode(landlordPage));
        await app.WithDb(async db => Assert.All(await db.Messages.ToListAsync(), message => Assert.False(message.IsRead)));
        using var readTenant = await app.PostWithToken("/Landlord/Messages/Read", new()
        {
            ["ConversationId"] = "90", ["MessageIds"] = "901"
        }, "/Landlord/Messages?c=90");
        Assert.Equal(HttpStatusCode.Redirect, readTenant.StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-User"); app.Client.DefaultRequestHeaders.Add("X-Test-User", TenantTestApp.TenantA);
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role"); app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Tenant");
        Assert.Contains("Đã xem", WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/Dashboard/Messages?ConversationId=90")));
        await app.WithDb(async db => Assert.False((await db.Messages.FindAsync(902))!.IsRead));
        using var readLandlord = await app.PostWithToken("/Tenant/Dashboard/ReadMessages", new()
        {
            ["ConversationId"] = "90", ["MessageIds"] = "902"
        }, "/Tenant/Dashboard/Messages?ConversationId=90");
        Assert.Equal(HttpStatusCode.Redirect, readLandlord.StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-User"); app.Client.DefaultRequestHeaders.Add("X-Test-User", "landlord");
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role"); app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Landlord");
        Assert.Contains("Đã xem", WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Landlord/Messages?c=90")));
    }

    [Fact]
    public async Task Hub_negotiation_requires_authentication_and_a_chat_role()
    {
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        Assert.Equal(HttpStatusCode.Unauthorized,(await app.Client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1",null)).StatusCode);
        app.Client.DefaultRequestHeaders.Add("X-Test-Role","Viewer");
        Assert.Equal(HttpStatusCode.Forbidden,(await app.Client.PostAsync("/hubs/chat/negotiate?negotiateVersion=1",null)).StatusCode);
    }

    [Fact]
    public async Task Shared_chat_controller_and_service_support_all_three_roles_and_enforce_membership_and_csrf()
    {
        await app.WithDb(async db =>
        {
            db.Users.Add(new ApplicationUser { Id = "admin-1", UserName = "admin-1", FullName = "Admin EZ Rent", Status = UserStatus.Active });
            db.Conversations.AddRange(
                new Conversation { ConversationId = 91, InitiatorId = TenantTestApp.TenantA, RecipientId = "admin-1", ConversationType = ConversationType.TenantAdmin },
                new Conversation { ConversationId = 92, InitiatorId = "landlord", RecipientId = "admin-1", ConversationType = ConversationType.LandlordAdmin });
            db.Messages.AddRange(
                new Message { MessageId = 910, ConversationId = 91, SenderId = TenantTestApp.TenantA, Content = "Hỗ trợ người thuê" },
                new Message { MessageId = 920, ConversationId = 92, SenderId = "landlord", Content = "Hỗ trợ chủ trọ" });
            await db.SaveChangesAsync();
        });

        app.Client.DefaultRequestHeaders.Remove("X-Test-User"); app.Client.DefaultRequestHeaders.Add("X-Test-User", "admin-1");
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role"); app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");

        var convJson = System.Text.RegularExpressions.Regex.Unescape(await app.Client.GetStringAsync("/Chat/Conversations"));
        Assert.Contains("Hỗ trợ người thuê", convJson);
        Assert.Contains("Hỗ trợ chủ trọ", convJson);

        var msgJson = System.Text.RegularExpressions.Regex.Unescape(await app.Client.GetStringAsync("/Chat/Messages/91"));
        Assert.Contains("Hỗ trợ người thuê", msgJson);
        await app.WithDb(async db => Assert.False((await db.Messages.FindAsync(910))!.IsRead));

        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsync("/Chat/Send/91", new FormUrlEncodedContent(new Dictionary<string, string> { ["content"] = "No CSRF" }))).StatusCode);

        using var adminReply = await app.PostWithToken("/Chat/Send/91", new() { ["content"] = "Admin đã phản hồi" }, "/Admin/RoleManager/Create");
        Assert.Equal(HttpStatusCode.OK, adminReply.StatusCode);

        using var adminRead = await app.PostWithToken("/Chat/Read", new() { ["conversationId"] = "91", ["messageIds"] = "910" }, "/Admin/RoleManager/Create");
        Assert.Equal(HttpStatusCode.OK, adminRead.StatusCode);
        await app.WithDb(async db => Assert.True((await db.Messages.FindAsync(910))!.IsRead));

        app.Client.DefaultRequestHeaders.Remove("X-Test-User"); app.Client.DefaultRequestHeaders.Add("X-Test-User", TenantTestApp.TenantB);
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role"); app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Tenant");
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync("/Chat/Messages/91")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.PostWithToken("/Chat/Send/91", new() { ["content"] = "Forged" })).StatusCode);
    }

    [Fact]
    public void Sql_server_migration_script_keeps_search_history_and_tenant_documents_without_connecting()
    {
        // These APIs generate SQL from source metadata only; never open a connection to a group database.
        var options = new DbContextOptionsBuilder<EZ_Rent.Data.AppDbContext>()
            .UseSqlServer("Server=unused;Database=unused;Integrated Security=true;TrustServerCertificate=true").Options;
        using var db = new EZ_Rent.Data.AppDbContext(options);
        var sql = db.GetService<IMigrator>().GenerateScript(options: MigrationsSqlGenerationOptions.Idempotent);
        Assert.Contains("SearchHistories", sql);
        Assert.Contains("ContractDocuments", sql);
        Assert.Contains("ContractTenantDrafts", sql);
        Assert.Contains("InvoiceMeterEvidence", sql);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
