using System.Net;
using System.Text.RegularExpressions;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantMessageWriteTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    private const string Inbox = "/Tenant/Dashboard/Messages";
    private const string Send = "/Tenant/Dashboard/SendMessage";
    private const string Read = "/Tenant/Dashboard/ReadMessages";
    public async Task InitializeAsync()
    {
        await app.InitializeAsync();
        await app.WithDb(async db =>
        {
            db.Conversations.AddRange(
                new Conversation { ConversationId = 1, InitiatorId = "landlord", RecipientId = TenantTestApp.TenantA, RentalPostId = 3, ConversationType = ConversationType.TenantLandlord },
                new Conversation { ConversationId = 2, InitiatorId = TenantTestApp.TenantB, RecipientId = "landlord", RentalPostId = 3, ConversationType = ConversationType.TenantLandlord },
                new Conversation { ConversationId = 3, InitiatorId = TenantTestApp.TenantA, RecipientId = "landlord", ConversationType = ConversationType.TenantAdmin },
                new Conversation { ConversationId = 4, InitiatorId = TenantTestApp.TenantA, RecipientId = "landlord", ConversationType = ConversationType.LandlordAdmin });
            await db.SaveChangesAsync();
            db.Messages.AddRange(
                new Message { MessageId = 1, ConversationId = 1, SenderId = "landlord", Content = "Incoming one" },
                new Message { MessageId = 2, ConversationId = 1, SenderId = "landlord", Content = "Incoming two" },
                new Message { MessageId = 3, ConversationId = 1, SenderId = TenantTestApp.TenantA, Content = "Own reply" },
                new Message { MessageId = 4, ConversationId = 2, SenderId = "landlord", Content = "Other tenant secret" },
                new Message { MessageId = 5, ConversationId = 1, SenderId = TenantTestApp.TenantB, Content = "Malformed outsider message" });
            await db.SaveChangesAsync();
        });
    }
    public Task DisposeAsync() => app.DisposeAsync();
    private async Task<string> Token()
    {
        // TestServer's client has no cookie jar. Obtain a matching fresh token/cookie pair per POST.
        app.Client.DefaultRequestHeaders.Remove("Cookie");
        using var response = await app.Client.GetAsync(Inbox + "?ConversationId=1");
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            app.Client.DefaultRequestHeaders.Remove("Cookie");
            app.Client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies.Select(c => c.Split(';')[0])));
        }
        return WebUtility.HtmlDecode(Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]*)\"").Groups[1].Value);
    }
    private async Task<HttpResponseMessage> Post(string action, Dictionary<string, string> values, bool ajax = false)
    {
        values["__RequestVerificationToken"] = await Token();
        var request = new HttpRequestMessage(HttpMethod.Post, action) { Content = new FormUrlEncodedContent(values) };
        if (ajax) request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await app.Client.SendAsync(request);
    }
    private async Task<HttpResponseMessage> ReadIds(int conversation, int[] ids)
    {
        var fields = new List<KeyValuePair<string, string>> { new("ConversationId", conversation.ToString()), new("__RequestVerificationToken", await Token()) };
        fields.AddRange(ids.Select(id => new KeyValuePair<string, string>("MessageIds", id.ToString())));
        var request = new HttpRequestMessage(HttpMethod.Post, Read) { Content = new FormUrlEncodedContent(fields) };
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");
        return await app.Client.SendAsync(request);
    }
    [Fact]
    public async Task Room_entry_opens_owned_existing_thread_without_writes()
    {
        var html = WebUtility.HtmlDecode(await app.Client.GetStringAsync(Inbox + "?RentalPostId=3"));
        Assert.Contains("data-conversation-id=\"1\"", html);
        Assert.Contains("Incoming one", html); Assert.DoesNotContain("Other tenant secret", html);
        Assert.Contains("Nhắn chủ nhà", WebUtility.HtmlDecode(await app.Client.GetStringAsync("/Tenant/RentalPost/Details/3")));
        await app.WithDb(async db => { Assert.Equal(4, await db.Conversations.CountAsync()); Assert.Equal(5, await db.Messages.CountAsync()); Assert.False((await db.Messages.FindAsync(1))!.IsRead); });
    }
    [Fact]
    public async Task Opening_new_room_does_not_create_empty_conversation_and_first_send_uses_server_owner()
    {
        await app.WithDb(async db => { var r = (await db.RentalPosts.FindAsync(6))!; r.RoomStatus = RoomStatus.Available; await db.SaveChangesAsync(); });
        var html = await app.Client.GetStringAsync(Inbox + "?RentalPostId=6");
        Assert.Contains("data-draft-key=\"room-6\"", html);
        var stamp = Regex.Match(html,"name=\"ContactStamp\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.Equal(64,stamp.Length);
        await app.WithDb(async db => Assert.Equal(4, await db.Conversations.CountAsync()));
        var response = await Post(Send, new() { ["RentalPostId"] = "6", ["ContactStamp"] = stamp, ["Content"] = "  Xin chào chủ nhà  ", ["SenderId"] = TenantTestApp.TenantB, ["RecipientId"] = TenantTestApp.TenantB, ["IsRead"] = "true", ["SentAt"] = "2000-01-01" });
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await app.WithDb(async db =>
        {
            var c = await db.Conversations.SingleAsync(c => c.RentalPostId == 6);
            Assert.Equal(TenantTestApp.TenantA, c.InitiatorId); Assert.Equal("landlord", c.RecipientId);
            var m = await db.Messages.SingleAsync(m => m.ConversationId == c.ConversationId);
            Assert.Equal("Xin chào chủ nhà", m.Content); Assert.Equal(TenantTestApp.TenantA, m.SenderId); Assert.False(m.IsRead);
            Assert.True(m.SentAt > DateTime.UtcNow.AddMinutes(-1)); Assert.NotNull(c.UpdatedAt);
        });
        // A stale page still targeting the room reuses the created conversation.
        Assert.Equal(HttpStatusCode.Redirect, (await Post(Send, new() { ["RentalPostId"] = "6", ["ContactStamp"] = stamp, ["Content"] = "Tin tiếp theo" })).StatusCode);
        await app.WithDb(async db => { Assert.Equal(5, await db.Conversations.CountAsync()); Assert.Equal(2, await db.Messages.CountAsync(m => m.Conversation.RentalPostId == 6)); });
    }
    [Fact]
    public async Task First_send_requires_contact_stamp_and_blocks_owner_changed_since_opening()
    {
        await app.WithDb(async db => { (await db.RentalPosts.FindAsync(6))!.RoomStatus = RoomStatus.Available; await db.SaveChangesAsync(); });
        var html = await app.Client.GetStringAsync(Inbox + "?RentalPostId=6");
        var stamp = Regex.Match(html,"name=\"ContactStamp\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.Equal(HttpStatusCode.BadRequest,(await Post(Send,new(){["RentalPostId"]="6",["Content"]="No stamp"},true)).StatusCode);
        await app.WithDb(async db => { (await db.RentalPosts.FindAsync(6))!.OwnerId = TenantTestApp.TenantB; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Conflict,(await Post(Send,new(){["RentalPostId"]="6",["ContactStamp"]=stamp,["Content"]="Old recipient"},true)).StatusCode);
        await app.WithDb(async db => { Assert.Equal(4,await db.Conversations.CountAsync());Assert.Equal(5,await db.Messages.CountAsync()); });
    }
    [Fact]
    public async Task Send_existing_persists_encoded_text_and_does_not_mark_other_incoming_read()
    {
        var response = await Post(Send, new() { ["ConversationId"] = "1", ["Content"] = "<script>alert(1)</script>\nDòng thứ hai" }, true);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Contains("ConversationId=1", await response.Content.ReadAsStringAsync());
        var html = await app.Client.GetStringAsync(Inbox + "?ConversationId=1");
        Assert.Contains("&lt;script&gt;", html); Assert.DoesNotContain("<script>alert(1)", html);
        await app.WithDb(async db => { Assert.Equal(6, await db.Messages.CountAsync()); Assert.False((await db.Messages.FindAsync(1))!.IsRead); });
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(999)]
    public async Task New_contact_rejects_nonpublic_unavailable_or_missing_room(int id)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync(Inbox + "?RentalPostId=" + id)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(Send, new() { ["RentalPostId"] = id.ToString(), ["Content"] = "Hello" }, true)).StatusCode);
        await app.WithDb(async db => Assert.Equal(5, await db.Messages.CountAsync()));
    }
    [Theory]
    [InlineData(2, HttpStatusCode.NotFound)] [InlineData(3, HttpStatusCode.Conflict)]
    [InlineData(4, HttpStatusCode.NotFound)] [InlineData(999, HttpStatusCode.NotFound)]
    public async Task Cannot_send_outside_owned_landlord_thread(int id, HttpStatusCode expected)
    {
        Assert.Equal(expected, (await Post(Send, new() { ["ConversationId"] = id.ToString(), ["Content"] = "Forbidden" }, true)).StatusCode);
        await app.WithDb(async db => Assert.Equal(5, await db.Messages.CountAsync()));
    }
    [Fact]
    public async Task Existing_chat_can_continue_when_room_is_hidden_without_revealing_room_details()
    {
        await app.WithDb(async db => { var r = (await db.RentalPosts.FindAsync(3))!; r.ApprovalStatus = ApprovalStatus.Draft; r.Title = "HIDDEN ROOM SECRET"; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.OK, (await Post(Send, new() { ["ConversationId"] = "1", ["Content"] = "Trao đổi tiếp" }, true)).StatusCode);
        var html = await app.Client.GetStringAsync(Inbox + "?ConversationId=1");
        Assert.DoesNotContain("HIDDEN ROOM SECRET", html);
    }
    [Theory]
    [InlineData(TenantTestApp.TenantA)] [InlineData("landlord")]
    public async Task Inactive_participant_makes_existing_thread_readonly(string id)
    {
        await app.WithDb(async db => { (await db.Users.FindAsync(id))!.Status = UserStatus.Locked; await db.SaveChangesAsync(); });
        var html = await app.Client.GetStringAsync(Inbox + "?ConversationId=1"); Assert.DoesNotContain("id=\"message-send-form\"", html);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(Send, new() { ["ConversationId"] = "1", ["Content"] = "Blocked" }, true)).StatusCode);
    }
    [Fact]
    public async Task Own_room_cannot_be_contacted()
    {
        await app.WithDb(async db => { (await db.RentalPosts.FindAsync(3))!.OwnerId = TenantTestApp.TenantA; await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync(Inbox + "?RentalPostId=3")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(Send, new() { ["RentalPostId"] = "3", ["Content"] = "Self" }, true)).StatusCode);
    }
    [Theory]
    [InlineData("")] [InlineData("   ")] [InlineData("too-long")]
    public async Task Invalid_content_does_not_write_and_no_js_keeps_attempted_content(string content)
    {
        if (content == "too-long") content = new string('x', 2001);
        var result = await Post(Send, new() { ["ConversationId"] = "1", ["Content"] = content });
        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        var html = await result.Content.ReadAsStringAsync(); Assert.Contains("message-send-form", html);
        if (content.Length > 0) Assert.Contains(content, html);
        await app.WithDb(async db => Assert.Equal(5, await db.Messages.CountAsync()));
    }
    [Fact]
    public async Task Exact_limit_and_both_target_validation_are_enforced()
    {
        Assert.Equal(HttpStatusCode.OK, (await Post(Send, new() { ["ConversationId"] = "1", ["Content"] = new string('a', 2000) }, true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(Send, new() { ["ConversationId"] = "1", ["RentalPostId"] = "3", ["Content"] = "Invalid" }, true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(Send, new() { ["Content"] = "Invalid" }, true)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.GetAsync(Inbox + "?ConversationId=1&RentalPostId=3")).StatusCode);
    }
    [Theory]
    [InlineData(Send)] [InlineData(Read)]
    public async Task Writes_require_antiforgery(string url)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string,string> { ["ConversationId"] = "1", ["Content"] = "No token", ["MessageIds"] = "1" }))).StatusCode);
    }
    [Theory]
    [InlineData(null, "Tenant")] [InlineData(TenantTestApp.TenantA, "Admin")] [InlineData(TenantTestApp.TenantA, "Landlord")]
    public async Task Writes_require_tenant_role_and_identity(string? id, string role)
    {
        var token = await Token(); app.Client.DefaultRequestHeaders.Remove("X-Test-User"); app.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        if (id != null) app.Client.DefaultRequestHeaders.Add("X-Test-User",id); app.Client.DefaultRequestHeaders.Add("X-Test-Role",role);
        foreach (var url in new[] {Send,Read})
            Assert.Equal(id == null ? HttpStatusCode.BadRequest : HttpStatusCode.Forbidden,(await app.Client.PostAsync(url,new FormUrlEncodedContent(new Dictionary<string,string>{["ConversationId"]="1",["Content"]="Forbidden",["MessageIds"]="1",["__RequestVerificationToken"]=token}))).StatusCode);
    }
    [Fact]
    public async Task Read_updates_only_requested_incoming_member_messages_and_is_idempotent()
    {
        var response = await ReadIds(1,[1,3,4,5,999]); Assert.Equal(HttpStatusCode.OK,response.StatusCode);
        Assert.Contains("\"unreadMessages\":1",await response.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK,(await ReadIds(1,[1])).StatusCode);
        await app.WithDb(async db =>
        {
            var rows = await db.Messages.OrderBy(m=>m.MessageId).ToListAsync();
            Assert.True(rows[0].IsRead); Assert.All(rows.Skip(1),m=>Assert.False(m.IsRead));
        });
    }
    [Fact]
    public async Task Reading_does_not_mark_a_new_arrival_or_another_page_and_rejects_other_thread()
    {
        _ = await Token();
        await app.WithDb(async db=> { db.Messages.Add(new Message {MessageId=6,ConversationId=1,SenderId="landlord",Content="Arrived later"});await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.OK,(await ReadIds(1,[1])).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,(await ReadIds(2,[4])).StatusCode);
        await app.WithDb(async db=> { Assert.False((await db.Messages.FindAsync(6))!.IsRead);Assert.False((await db.Messages.FindAsync(2))!.IsRead); });
    }
    [Fact]
    public async Task Read_rejects_empty_oversized_and_invalid_id_batches()
    {
        foreach(var ids in new[]{Array.Empty<int>(),new[]{0},Enumerable.Range(1,41).ToArray()})
            Assert.Equal(HttpStatusCode.BadRequest,(await ReadIds(1,ids)).StatusCode);
    }
}
