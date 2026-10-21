using System.Net;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Areas.Tenant.ViewModels;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantMessagesTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    public async Task InitializeAsync()
    {
        await app.InitializeAsync();
        await app.WithDb(async db =>
        {
            db.Conversations.AddRange(
                new Conversation { ConversationId = 1, InitiatorId = TenantTestApp.TenantA, RecipientId = "landlord", RentalPostId = 3, ConversationType = ConversationType.TenantLandlord },
                new Conversation { ConversationId = 2, InitiatorId = "landlord", RecipientId = TenantTestApp.TenantA, ConversationType = ConversationType.TenantAdmin },
                new Conversation { ConversationId = 3, InitiatorId = TenantTestApp.TenantB, RecipientId = "landlord", RentalPostId = 3, ConversationType = ConversationType.TenantLandlord },
                new Conversation { ConversationId = 4, InitiatorId = TenantTestApp.TenantA, RecipientId = "landlord", ConversationType = ConversationType.LandlordAdmin });
            await db.SaveChangesAsync();
            db.Messages.AddRange(
                new Message { MessageId = 1, ConversationId = 1, SenderId = "landlord", Content = "OWN MESSAGE", SentAt = new DateTime(2026, 10, 16, 18, 30, 0) },
                new Message { MessageId = 2, ConversationId = 1, SenderId = TenantTestApp.TenantA, Content = "MY REPLY", IsRead = true, SentAt = new DateTime(2026, 10, 16, 18, 31, 0) },
                new Message { MessageId = 3, ConversationId = 2, SenderId = "landlord", Content = "SUPPORT REPLY", IsRead = true },
                new Message { MessageId = 4, ConversationId = 3, SenderId = "landlord", Content = "OTHER TENANT SECRET" },
                new Message { MessageId = 5, ConversationId = 4, SenderId = "landlord", Content = "WRONG CONVERSATION KIND" },
                new Message { MessageId = 6, ConversationId = 1, SenderId = TenantTestApp.TenantB, Content = "NONMEMBER MESSAGE", SentAt = DateTime.MaxValue.AddDays(-1) });
            await db.SaveChangesAsync();
        });
    }
    public Task DisposeAsync() => app.DisposeAsync();
    private async Task<string> Html(string query = "")
    {
        var response = await app.Client.GetAsync("/Tenant/Dashboard/Messages" + query);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Membership_kind_and_message_sender_are_scoped_before_rendering()
    {
        var html = await Html("?ConversationId=1");
        Assert.Contains("OWN MESSAGE", html); Assert.Contains("MY REPLY", html);
        Assert.DoesNotContain("OTHER TENANT SECRET", html); Assert.DoesNotContain("WRONG CONVERSATION KIND", html);
        Assert.DoesNotContain("NONMEMBER MESSAGE", html);
        Assert.Contains("data-mobile-chat=\"true\"", html);
        foreach (var id in new[] { 3, 4, 999 })
        {
            using var response = await app.Client.GetAsync($"/Tenant/Dashboard/Messages?ConversationId={id}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.DoesNotContain("SECRET", await response.Content.ReadAsStringAsync());
        }
        Assert.Contains("SUPPORT REPLY", await Html("?ConversationId=2"));
    }

    [Theory]
    [InlineData(null, "Tenant")]
    [InlineData("tenant-a-private", "Admin")]
    [InlineData("tenant-a-private", "Landlord")]
    public async Task Identity_and_role_are_required(string? id, string role)
    {
        app.Client.DefaultRequestHeaders.Remove("X-Test-User");
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        if (id != null) app.Client.DefaultRequestHeaders.Add("X-Test-User", id);
        app.Client.DefaultRequestHeaders.Add("X-Test-Role", role);
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync("/Tenant/Dashboard/Messages")).StatusCode);
    }

    [Fact]
    public async Task Read_does_not_mark_messages_read_or_create_records_and_no_post_is_available()
    {
        await Html("?ConversationId=1"); await Html("?ConversationId=2");
        await app.WithDb(async db =>
        {
            Assert.False((await db.Messages.AsNoTracking().SingleAsync(m => m.MessageId == 1)).IsRead);
            Assert.Equal(6, await db.Messages.CountAsync()); Assert.Equal(4, await db.Conversations.CountAsync());
        });
        Assert.Equal(HttpStatusCode.MethodNotAllowed,
            (await app.Client.PostAsync("/Tenant/Dashboard/Messages", new FormUrlEncodedContent(new Dictionary<string,string> { ["Content"] = "No write" }))).StatusCode);
    }

    [Fact]
    public async Task Unread_filter_and_reversed_participant_mapping_use_original_data()
    {
        await app.WithDb(async db =>
        {
            var input = new TenantMessagesInput { Filter = "unread" };
            var model = await TenantMessagesReader.ReadAsync(db, TenantTestApp.TenantA, input);
            Assert.Equal(1, model.UnreadCount); Assert.Single(model.Conversations); Assert.Equal(1, model.Conversations[0].Id);
            Assert.Equal("Landlord", model.Conversations[0].Name);
            model = await TenantMessagesReader.ReadAsync(db, TenantTestApp.TenantA, new() { ConversationId = 2 });
            Assert.Equal("Hỗ trợ EZ Rent", model.Selected?.Name); Assert.True(model.Selected?.IsSupport);
            Assert.Equal("SUPPORT REPLY", Assert.Single(model.Messages).Content);
        });
    }

    [Fact]
    public async Task Hidden_room_is_masked_search_does_not_reveal_it_and_unsafe_image_is_rejected()
    {
        await app.WithDb(async db =>
        {
            var room = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 3);
            room.Title = "SECRET HIDDEN ROOM"; room.Address = "SECRET ADDRESS"; room.ApprovalStatus = ApprovalStatus.Draft;
            await db.SaveChangesAsync();
        });
        var html = await Html("?ConversationId=1");
        Assert.DoesNotContain("SECRET HIDDEN ROOM", html); Assert.DoesNotContain("SECRET ADDRESS", html);
        await app.WithDb(async db =>
        {
            var model = await TenantMessagesReader.ReadAsync(db, TenantTestApp.TenantA, new() { Query = "SECRET HIDDEN ROOM" });
            Assert.Empty(model.Conversations);
            var room = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 3); room.ApprovalStatus = ApprovalStatus.Approved;
            db.RoomImages.Add(new RoomImage { RentalPostId = 3, MediaUrl = "javascript:alert(1)", IsPrimary = true }); await db.SaveChangesAsync();
            model = await TenantMessagesReader.ReadAsync(db, TenantTestApp.TenantA, new() { ConversationId = 1 });
            Assert.Equal(RentalPresentation.Placeholder, model.Selected?.Room?.ImageUrl);
        });
    }

    [Fact]
    public async Task Numbered_conversation_and_history_pages_are_bounded_and_ties_are_stable()
    {
        await app.WithDb(async db =>
        {
            var sent = new DateTime(2026, 10, 15);
            for (var i = 10; i <= 21; i++) db.Conversations.Add(new Conversation { ConversationId = i, InitiatorId = TenantTestApp.TenantA, RecipientId = "landlord", ConversationType = ConversationType.TenantLandlord, CreatedAt = sent });
            for (var i = 10; i <= 94; i++) db.Messages.Add(new Message { MessageId = i, ConversationId = 1, SenderId = "landlord", Content = "HISTORY " + i, SentAt = sent });
            await db.SaveChangesAsync();
            var model = await TenantMessagesReader.ReadAsync(db, TenantTestApp.TenantA, new() { ConversationId = 1 });
            Assert.Equal(10, model.Conversations.Count); Assert.Equal(2, model.TotalPages);
            Assert.Equal(40, model.Messages.Count); Assert.Equal(3, model.MessagePages);
            Assert.Equal(57, model.Messages.First().Id); Assert.Equal(2, model.Messages.Last().Id);
            var input = new TenantMessagesInput { ConversationId = 1, MessagePage = int.MaxValue, Page = int.MaxValue };
            model = await TenantMessagesReader.ReadAsync(db, TenantTestApp.TenantA, input);
            Assert.Equal(2, input.Page); Assert.Equal(3, input.MessagePage); Assert.Equal(7, model.Messages.Count);
            Assert.Equal(10, model.Messages.First().Id); Assert.Equal(16, model.Messages.Last().Id);
        });
        var html = await Html("?ConversationId=1&MessagePage=2");
        Assert.Contains("tenant-pagination", html); Assert.Contains("MessagePage=3", WebUtility.HtmlDecode(html));
    }

    [Theory]
    [InlineData("?ConversationId=-1")]
    [InlineData("?Page=abc")]
    [InlineData("?MessagePage=0")]
    [InlineData("?Filter=other")]
    [InlineData("?View=other")]
    public async Task Invalid_input_has_recoverable_400(string query)
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.GetAsync("/Tenant/Dashboard/Messages" + query)).StatusCode);
    }

    [Fact]
    public async Task Razor_encodes_content_and_utc_message_dates_display_in_vietnam()
    {
        await app.WithDb(async db =>
        {
            (await db.Messages.SingleAsync(m => m.MessageId == 1)).Content = "<img src=x onerror=alert(1)>";
            (await db.Users.SingleAsync(u => u.Id == "landlord")).FullName = "<script>name()</script>";
            await db.SaveChangesAsync();
        });
        var html = await Html("?ConversationId=1");
        Assert.Contains("&lt;img", html); Assert.Contains("&lt;script&gt;", html);
        Assert.DoesNotContain("<img src=x", html); Assert.DoesNotContain("<script>name()", html);
        Assert.Contains("2026-10-17T01:30:00+07:00", WebUtility.HtmlDecode(html));
        Assert.Contains("/Tenant/Dashboard/SendMessage", WebUtility.HtmlDecode(html));
        Assert.Contains("aria-label=\"Gửi tin nhắn\"", WebUtility.HtmlDecode(html));
        Assert.Contains("/Tenant/Support/Create", html);
    }
}
