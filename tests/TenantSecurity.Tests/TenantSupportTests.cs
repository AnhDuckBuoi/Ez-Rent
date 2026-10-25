using System.Net;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantSupportTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    private const string Create = "/Tenant/Support/Create";
    private const string Inbox = "/Tenant/Dashboard/Messages";
    public async Task InitializeAsync()
    {
        await app.InitializeAsync();
        await app.WithDb(async db =>
        {
            db.Users.Add(new ApplicationUser { Id = "admin-a", UserName = "admin-a", FullName = "Admin A" });
            db.Roles.Add(new IdentityRole { Id = "admin-role", Name = "Admin", NormalizedName = "ADMIN" });
            db.UserRoles.Add(new IdentityUserRole<string> { UserId = "admin-a", RoleId = "admin-role" });
            await db.SaveChangesAsync();
        });
    }
    public Task DisposeAsync() => app.DisposeAsync();
    private static Dictionary<string, string> Fields() => new()
    {
        ["Input.Category"] = "room-info", ["Input.Subject"] = "Thông tin cần làm rõ",
        ["Input.Description"] = "Nội dung hỗ trợ riêng của tôi"
    };
    private Task<HttpResponseMessage> Post(Dictionary<string, string>? fields = null) => app.PostWithToken(Create, fields ?? Fields(), Create);
    private async Task<int> NewRequest()
    {
        using var response = await Post();
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Filter=support", response.Headers.Location!.OriginalString);
        var id = 0;
        await app.WithDb(async db => id = await db.Conversations.MaxAsync(c => c.ConversationId));
        return id;
    }

    [Fact]
    public async Task Form_get_is_read_only_and_links_use_real_routes()
    {
        using var response = await app.Client.GetAsync(Create + "?rentalPostId=3");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl!.ToString());
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("__RequestVerificationToken", html); Assert.Contains("Room 3", html);
        Assert.DoesNotContain("Room 2", html); Assert.DoesNotContain("Room 5", html);
        Assert.Contains("/Tenant/Support/Create", WebUtility.HtmlDecode(await app.Client.GetStringAsync(Inbox)));
        Assert.DoesNotContain("Sắp có", WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/Dashboard/Help")));
        await app.WithDb(async db => { Assert.Equal(0, await db.Conversations.CountAsync()); Assert.Equal(0, await db.Messages.CountAsync()); });
    }

    [Fact]
    public async Task First_post_uses_server_identity_and_admin_role_without_creating_a_repair()
    {
        var fields = Fields(); fields["Input.RentalPostId"] = "3";
        fields["RecipientId"] = fields["Input.RecipientId"] = TenantTestApp.TenantB;
        fields["SenderId"] = TenantTestApp.TenantB; fields["IsRead"] = "true"; fields["Status"] = "Resolved";
        using var response = await Post(fields);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await app.WithDb(async db =>
        {
            var conversation = await db.Conversations.SingleAsync();
            Assert.Equal(ConversationType.TenantAdmin, conversation.ConversationType);
            Assert.Equal("admin-a", conversation.RecipientId); Assert.Equal(TenantTestApp.TenantA, conversation.InitiatorId);
            Assert.Equal(3, conversation.RentalPostId);
            var message = await db.Messages.SingleAsync();
            Assert.Equal(TenantTestApp.TenantA, message.SenderId); Assert.False(message.IsRead);
            Assert.StartsWith("Thông tin cần làm rõ\nVấn đề: Thông tin phòng không đúng", message.Content);
            Assert.Contains("Nội dung hỗ trợ riêng của tôi", message.Content);
            Assert.True(message.SentAt >= DateTime.UtcNow.AddMinutes(-1));
            Assert.Equal(2, await db.RepairRequests.CountAsync());
        });
    }

    [Fact]
    public async Task Maximum_lengths_fit_original_message_and_distinct_requests_stay_separate()
    {
        var fields = Fields(); fields["Input.Subject"] = new string('T', 100); fields["Input.Description"] = new string('D', 1000);
        Assert.Equal(HttpStatusCode.Redirect, (await Post(fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await Post()).StatusCode);
        await app.WithDb(async db => { Assert.Equal(2, await db.Conversations.CountAsync()); Assert.All(await db.Messages.ToListAsync(), m => Assert.InRange(m.Content.Length, 1, 2000)); });
    }

    [Theory]
    [InlineData("Input.Category", "forged-category")]
    [InlineData("Input.Category", "")]
    [InlineData("Input.Subject", "   ")]
    [InlineData("Input.Description", "")]
    [InlineData("Input.RentalPostId", "0")]
    [InlineData("Input.RentalPostId", "abc")]
    public async Task Invalid_form_does_not_write_and_keeps_other_fields(string field, string value)
    {
        var fields = Fields(); fields[field] = value;
        using var response = await Post(fields);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains(field == "Input.Description" ? "Thông tin cần làm rõ" : "Nội dung hỗ trợ riêng của tôi", html);
        Assert.Contains("field-validation-error", html);
        await app.WithDb(async db => Assert.Equal(0, await db.Conversations.CountAsync()));
    }

    [Theory]
    [InlineData("Input.Subject", 101)]
    [InlineData("Input.Description", 1001)]
    public async Task Overlong_content_is_rejected_without_truncating_the_attempted_form(string field, int length)
    {
        var fields = Fields(); fields[field] = new string('Z', length);
        using var response = await Post(fields);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains(new string('Z', length), await response.Content.ReadAsStringAsync());
        await app.WithDb(async db => Assert.Equal(0, await db.Messages.CountAsync()));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(999)]
    public async Task Foreign_private_draft_hidden_or_missing_room_cannot_be_attached(int roomId)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync(Create + "?rentalPostId=" + roomId)).StatusCode);
        var fields = Fields(); fields["Input.RentalPostId"] = roomId.ToString();
        Assert.Equal(HttpStatusCode.Conflict, (await Post(fields)).StatusCode);
        await app.WithDb(async db => Assert.Equal(0, await db.Conversations.CountAsync()));
    }

    [Fact]
    public async Task Own_active_rented_room_can_be_related_but_a_stale_public_room_is_rechecked()
    {
        var fields = Fields(); fields["Input.RentalPostId"] = "1";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(fields)).StatusCode);
        await app.WithDb(async db => { (await db.RentalPosts.FindAsync(3))!.ApprovalStatus = ApprovalStatus.Draft; await db.SaveChangesAsync(); });
        fields["Input.RentalPostId"] = "3";
        Assert.Equal(HttpStatusCode.Conflict, (await Post(fields)).StatusCode);
        await app.WithDb(async db => Assert.Equal(1, await db.Conversations.CountAsync()));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("locked")]
    [InlineData("revoked")]
    public async Task No_active_admin_means_no_fake_success_or_partial_conversation(string mode)
    {
        await app.WithDb(async db =>
        {
            if (mode == "locked") (await db.Users.FindAsync("admin-a"))!.Status = UserStatus.Locked;
            else if (mode == "missing") db.Users.Remove((await db.Users.FindAsync("admin-a"))!);
            else db.UserRoles.RemoveRange(await db.UserRoles.Where(r => r.UserId == "admin-a").ToListAsync());
            await db.SaveChangesAsync();
        });
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync(Create));
        Assert.Contains("Chưa có tài khoản quản trị hoạt động", html); Assert.Contains("disabled", html);
        using var response = await Post();
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("Nội dung hỗ trợ riêng của tôi", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        await app.WithDb(async db => Assert.Equal(0, await db.Conversations.CountAsync()));
    }

    [Fact]
    public async Task Support_followups_and_replies_are_owned_and_old_recipient_must_still_be_an_admin()
    {
        var id = await NewRequest();
        await app.WithDb(async db => { db.Messages.Add(new Message { ConversationId = id, SenderId = "admin-a", Content = "Phản hồi riêng" }); await db.SaveChangesAsync(); });
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync(Inbox + "?ConversationId=" + id));
        Assert.Contains("Phản hồi riêng", html); Assert.Contains("message-send-form", html);
        Assert.DoesNotContain("data-quick", html);
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Dashboard/SendMessage", new() { ["ConversationId"] = id.ToString(), ["Content"] = "Bổ sung nội dung" }, Create)).StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-User"); app.Client.DefaultRequestHeaders.Add("X-Test-User", TenantTestApp.TenantB);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync(Inbox + "?ConversationId=" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.PostWithToken("/Tenant/Dashboard/SendMessage", new() { ["ConversationId"] = id.ToString(), ["Content"] = "Giả mạo" }, Create)).StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-User"); app.Client.DefaultRequestHeaders.Add("X-Test-User", TenantTestApp.TenantA);
        await app.WithDb(async db => { db.UserRoles.RemoveRange(await db.UserRoles.ToListAsync()); await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Conflict, (await app.PostWithToken("/Tenant/Dashboard/SendMessage", new() { ["ConversationId"] = id.ToString(), ["Content"] = "Không được lưu" }, Create)).StatusCode);
        await app.WithDb(async db => Assert.Equal(3, await db.Messages.CountAsync()));
    }

    [Fact]
    public async Task Support_filter_is_numbered_scoped_and_read_get_has_no_side_effect()
    {
        await app.WithDb(async db =>
        {
            for (var i = 1; i <= 12; i++) db.Conversations.Add(new Conversation { InitiatorId = TenantTestApp.TenantA, RecipientId = "admin-a", ConversationType = ConversationType.TenantAdmin });
            db.Conversations.Add(new Conversation { InitiatorId = TenantTestApp.TenantB, RecipientId = "admin-a", ConversationType = ConversationType.TenantAdmin });
            db.Conversations.Add(new Conversation { InitiatorId = TenantTestApp.TenantA, RecipientId = "landlord", ConversationType = ConversationType.TenantLandlord });
            await db.SaveChangesAsync();
            var model = await TenantMessagesReader.ReadAsync(db, TenantTestApp.TenantA, new() { Filter = "support", Page = 2 });
            Assert.Equal(12, model.Total); Assert.Equal(2, model.TotalPages); Assert.Equal(2, model.Conversations.Count);
            Assert.All(model.Conversations, c => Assert.True(c.IsSupport));
        });
        var html = await app.Client.GetStringAsync(Inbox + "?Filter=support&Page=2&View=inbox");
        Assert.Contains("Filter=support", html); Assert.Contains("tenant-pagination", html);
        await app.WithDb(async db => Assert.Equal(0, await db.Messages.CountAsync()));
    }

    [Fact]
    public async Task Stored_and_failed_form_content_is_html_encoded()
    {
        var fields = Fields(); fields["Input.Subject"] = "<script>alert(1)</script>"; fields["Input.Description"] = "<img src=x onerror=alert(2)>";
        Assert.Equal(HttpStatusCode.Redirect, (await Post(fields)).StatusCode);
        var html = await app.Client.GetStringAsync(Inbox + "?Filter=support");
        Assert.Contains("&lt;script&gt;", html); Assert.Contains("&lt;img", html);
        Assert.DoesNotContain("<script>alert(1)", html); Assert.DoesNotContain("<img src=x", html);
        fields["Input.Category"] = "invalid";
        html = await (await Post(fields)).Content.ReadAsStringAsync();
        Assert.DoesNotContain("<script>alert(1)", html);
    }

    [Fact]
    public async Task Csrf_authentication_and_active_sender_are_required()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsync(Create, new FormUrlEncodedContent(Fields()))).StatusCode);
        await app.WithDb(async db => { (await db.Users.FindAsync(TenantTestApp.TenantA))!.Status = UserStatus.Locked; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Conflict, (await Post()).StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-User");
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync(Create)).StatusCode);
        app.Client.DefaultRequestHeaders.Add("X-Test-User", "landlord"); app.Client.DefaultRequestHeaders.Remove("X-Test-Role"); app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Landlord");
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync(Create)).StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync(Create)).StatusCode);
    }
}
