using System.Net;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace TenantSecurity.Tests;

// Each case gets a fresh in-memory relational database and its own two Tenant accounts.
public sealed class TenantSecurityTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    public Task InitializeAsync() => app.InitializeAsync();
    public Task DisposeAsync() => app.DisposeAsync();

    [Theory]
    [InlineData("Favorite")]
    [InlineData("ViewingAppointment")]
    [InlineData("Contract")]
    [InlineData("Invoice")]
    [InlineData("RepairRequest")]
    [InlineData("RentalPost")]
    [InlineData("Dashboard")]
    [InlineData("Notification")]
    [InlineData("ViewingBooking")]
    [InlineData("Account")]
    public async Task Authentication_role_and_identity_are_required(string controller)
    {
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        Assert.Equal(HttpStatusCode.Unauthorized, (await app.Client.GetAsync($"/Tenant/{controller}")).StatusCode);
        app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Landlord");
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync($"/Tenant/{controller}")).StatusCode);
        app.Client.DefaultRequestHeaders.Remove("X-Test-Role");
        app.Client.DefaultRequestHeaders.Add("X-Test-Role", "Tenant");
        app.Client.DefaultRequestHeaders.Remove("X-Test-User");
        Assert.Equal(HttpStatusCode.Forbidden, (await app.Client.GetAsync($"/Tenant/{controller}")).StatusCode);
    }

    [Theory]
    [InlineData("Favorite")]
    [InlineData("ViewingAppointment")]
    [InlineData("Contract")]
    [InlineData("Invoice")]
    [InlineData("RepairRequest")]
    public async Task Lists_and_details_are_scoped_to_the_logged_in_tenant(string controller)
    {
        using var index = await app.Client.GetAsync($"/Tenant/{controller}");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        var html = await index.Content.ReadAsStringAsync();
        if (controller is "Favorite" or "ViewingAppointment")
        {
            Assert.Contains("Address 3", html);
            Assert.DoesNotContain(TenantTestApp.TenantA, html);
        }
        else Assert.Contains(TenantTestApp.TenantA, html);
        Assert.DoesNotContain(TenantTestApp.TenantB, html);
        Assert.Equal(HttpStatusCode.OK, (await app.Client.GetAsync($"/Tenant/{controller}/Details/1")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync($"/Tenant/{controller}/Details/2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync($"/Tenant/{controller}/Details/9999")).StatusCode);
    }

    [Theory]
    [InlineData("Favorite")]
    [InlineData("ViewingAppointment")]
    [InlineData("RepairRequest")]
    public async Task Other_tenants_records_cannot_be_edited_or_deleted_even_with_a_valid_csrf_token(string controller)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync($"/Tenant/{controller}/Edit/2")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync($"/Tenant/{controller}/Delete/2")).StatusCode);
        var fields = new Dictionary<string, string>
        {
            ["RentalPostId"] = "3", ["TenantId"] = TenantTestApp.TenantA,
            ["UserId"] = TenantTestApp.TenantA, ["Description"] = "Attacker change",
            ["ProblemType"] = "Electricity", ["ViewingDate"] = "2027-01-20T10:00", ["Priority"] = "2"
        };
        Assert.Equal(HttpStatusCode.NotFound, (await app.PostWithToken($"/Tenant/{controller}/Edit/2", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await app.PostWithToken($"/Tenant/{controller}/Delete/2", new())).StatusCode);
        await app.WithDb(async db =>
        {
            Assert.Equal(TenantTestApp.TenantB, (await db.Favorites.SingleAsync(f => f.FavoriteId == 2)).UserId);
            Assert.Equal(ViewingAppointmentStatus.Pending, (await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 2)).Status);
            Assert.Equal("Broken light", (await db.RepairRequests.SingleAsync(r => r.RepairRequestId == 2)).Description);
        });
    }

    [Theory]
    [InlineData("Favorite")]
    [InlineData("ViewingAppointment")]
    [InlineData("RepairRequest")]
    public async Task Own_forms_work_and_do_not_expose_server_controlled_inputs(string controller)
    {
        foreach (var action in new[] { "Create", "Edit/1", "Delete/1" })
        {
            using var response = await app.Client.GetAsync($"/Tenant/{controller}/{action}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            foreach (var field in new[] { "TenantId", "UserId", "Status", "LandlordNote", "ResolutionNote", "CreatedAt", "UpdatedAt", "ResolvedAt" })
                Assert.DoesNotContain($"name=\"{field}\"", html);
        }
    }

    [Theory]
    [InlineData("Favorite")]
    [InlineData("ViewingAppointment")]
    [InlineData("RepairRequest")]
    public async Task Mutations_require_csrf_tokens(string controller)
    {
        foreach (var action in new[] { "Create", "Edit/1", "Delete/1" })
        {
            using var response = await app.Client.PostAsync($"/Tenant/{controller}/{action}", new FormUrlEncodedContent(new Dictionary<string, string>()));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Theory]
    [InlineData("Contract", "Create")]
    [InlineData("Contract", "Edit/1")]
    [InlineData("Contract", "Delete/1")]
    [InlineData("Invoice", "Create")]
    [InlineData("Invoice", "Edit/1")]
    [InlineData("Invoice", "Delete/1")]
    public async Task Financial_crud_endpoints_do_not_exist(string controller, string action)
    {
        Assert.Equal(HttpStatusCode.NotFound, (await app.Client.GetAsync($"/Tenant/{controller}/{action}")).StatusCode);
        var fields = new Dictionary<string, string>
        {
            ["TotalAmount"] = "0", ["MonthlyRent"] = "0", ["PaymentStatus"] = "3",
            ["Status"] = "4", ["PaidAt"] = "2000-01-01", ["TenantId"] = TenantTestApp.TenantA
        };
        Assert.Equal(HttpStatusCode.NotFound, (await app.PostWithToken($"/Tenant/{controller}/{action}", fields)).StatusCode);
        await app.WithDb(async db =>
        {
            Assert.Equal(3000000m, (await db.RentalContracts.SingleAsync(c => c.RentalContractId == 1)).MonthlyRent);
            var invoice = await db.Invoices.SingleAsync(i => i.InvoiceId == 1);
            Assert.Equal(3500000m, invoice.TotalAmount);
            Assert.Equal(PaymentStatus.Unpaid, invoice.PaymentStatus);
            Assert.Null(invoice.PaidAt);
        });
        var html = await (await app.Client.GetAsync($"/Tenant/{controller}")).Content.ReadAsStringAsync();
        foreach (var bannedAction in new[] { "Create", "Edit", "Delete" })
            Assert.DoesNotContain($"/Tenant/{controller}/{bannedAction}", html);
    }

    [Fact]
    public async Task Favorite_duplicate_is_idempotent_and_server_owns_identity_and_creation_time()
    {
        var fields = new Dictionary<string, string>
        {
            ["RentalPostId"] = "3", ["UserId"] = TenantTestApp.TenantB, ["CreatedAt"] = "2000-01-01",
            ["FavoriteId"] = "2"
        };
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Favorite/Create", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Favorite/Create", fields)).StatusCode);
        await app.WithDb(async db =>
        {
            Assert.Equal(2, await db.Favorites.CountAsync());
            db.RentalPosts.Add(new EZ_Rent.Models.RentalPost
            {
                RentalPostId = 7, Title = "Other available room", Description = "Test", Address = "Address 7",
                City = "City", OwnerId = "landlord", ApprovalStatus = ApprovalStatus.Approved
            });
            await db.SaveChangesAsync();
        });
        fields["RentalPostId"] = "7";
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Favorite/Create", fields)).StatusCode);
        await app.WithDb(async db =>
        {
            var created = await db.Favorites.SingleAsync(f => f.RentalPostId == 7);
            Assert.Equal(TenantTestApp.TenantA, created.UserId);
            Assert.True(created.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
        });
        Assert.Equal(HttpStatusCode.OK, (await app.PostWithToken("/Tenant/Favorite/Edit/1", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Favorite/Delete/3", new())).StatusCode);
        // Existing IDs come from the route, not a forged hidden entity ID.
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Favorite/Edit/1", fields)).StatusCode);
        await app.WithDb(async db =>
        {
            var edited = await db.Favorites.SingleAsync(f => f.FavoriteId == 1);
            Assert.Equal(7, edited.RentalPostId);
            Assert.Equal(TenantTestApp.TenantA, edited.UserId);
            Assert.True(edited.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
            Assert.Equal(3, (await db.Favorites.SingleAsync(f => f.FavoriteId == 2)).RentalPostId);
        });
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/Favorite/Delete/1", new())).StatusCode);
        await app.WithDb(async db => Assert.False(await db.Favorites.AnyAsync(f => f.FavoriteId == 1)));
    }

    [Theory]
    [InlineData("Favorite", 4)]
    [InlineData("Favorite", 5)]
    [InlineData("Favorite", 6)]
    [InlineData("Favorite", 999)]
    [InlineData("ViewingAppointment", 4)]
    [InlineData("ViewingAppointment", 5)]
    [InlineData("ViewingAppointment", 6)]
    [InlineData("ViewingAppointment", 999)]
    [InlineData("RepairRequest", 2)]
    [InlineData("RepairRequest", 3)]
    [InlineData("RepairRequest", 999)]
    public async Task Inaccessible_room_references_are_rejected(string controller, int postId)
    {
        var fields = new Dictionary<string, string>
        {
            ["RentalPostId"] = postId.ToString(), ["ViewingDate"] = "2027-01-20T10:00",
            ["ProblemType"] = "Electricity", ["Description"] = "Broken light", ["Priority"] = "2"
        };
        Assert.Equal(HttpStatusCode.OK, (await app.PostWithToken($"/Tenant/{controller}/Create", fields)).StatusCode);
        await app.WithDb(async db =>
        {
            Assert.Equal(2, await db.Favorites.CountAsync());
            Assert.Equal(2, await db.ViewingAppointments.CountAsync());
            Assert.Equal(2, await db.RepairRequests.CountAsync());
        });
    }

    [Fact]
    public async Task Appointment_create_and_edit_ignore_forged_server_fields_and_cancel_keeps_history()
    {
        var fields = new Dictionary<string, string>
        {
            ["RentalPostId"] = "3", ["ViewingDate"] = "2027-01-20T10:00", ["Note"] = "New tenant note",
            ["TenantId"] = TenantTestApp.TenantB, ["Status"] = "3", ["LandlordNote"] = "Forged",
            ["CreatedAt"] = "2000-01-01", ["UpdatedAt"] = "2000-01-01", ["ViewingAppointmentId"] = "2"
        };
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/ViewingAppointment/Create", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/ViewingAppointment/Edit/1", fields)).StatusCode);
        await app.WithDb(async db =>
        {
            var created = await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 3);
            Assert.Equal(TenantTestApp.TenantA, created.TenantId);
            Assert.Equal(ViewingAppointmentStatus.Pending, created.Status);
            Assert.Null(created.LandlordNote);
            Assert.Null(created.UpdatedAt);
            Assert.True(created.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
            var edited = await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 1);
            Assert.Equal("New tenant note", edited.Note);
            Assert.Equal("Private landlord note", edited.LandlordNote);
            Assert.Equal(TenantTestApp.TenantA, edited.TenantId);
            Assert.Equal(ViewingAppointmentStatus.Pending, edited.Status);
            Assert.True(edited.UpdatedAt > DateTime.UtcNow.AddMinutes(-5));
        });
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/ViewingAppointment/Delete/1", new())).StatusCode);
        await app.WithDb(async db =>
        {
            var cancelled = await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 1);
            Assert.Equal(ViewingAppointmentStatus.Cancelled, cancelled.Status);
            Assert.Equal("Private landlord note", cancelled.LandlordNote);
        });
        Assert.Equal(HttpStatusCode.Conflict, (await app.PostWithToken("/Tenant/ViewingAppointment/Edit/1", fields)).StatusCode);
    }

    [Fact]
    public async Task Repair_create_edit_and_cancel_protect_landlord_fields_and_history()
    {
        var fields = new Dictionary<string, string>
        {
            ["RentalPostId"] = "1", ["ProblemType"] = "Water", ["Description"] = "Broken tap", ["Priority"] = "3",
            ["TenantId"] = TenantTestApp.TenantB, ["Status"] = "3", ["ResolutionNote"] = "Forged",
            ["ResolvedAt"] = "2000-01-01", ["CreatedAt"] = "2000-01-01", ["RepairRequestId"] = "2"
        };
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/RepairRequest/Create", fields)).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/RepairRequest/Edit/1", fields)).StatusCode);
        await app.WithDb(async db =>
        {
            var created = await db.RepairRequests.SingleAsync(r => r.RepairRequestId == 3);
            Assert.Equal(TenantTestApp.TenantA, created.TenantId);
            Assert.Equal(RepairRequestStatus.Pending, created.Status);
            Assert.Null(created.ResolvedAt);
            Assert.Null(created.ResolutionNote);
            Assert.True(created.CreatedAt > DateTime.UtcNow.AddMinutes(-5));
            var edited = await db.RepairRequests.SingleAsync(r => r.RepairRequestId == 1);
            Assert.Equal("Broken tap", edited.Description);
            Assert.Equal("Landlord only", edited.ResolutionNote);
            Assert.Equal(TenantTestApp.TenantA, edited.TenantId);
            Assert.Null(edited.ResolvedAt);
        });
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/RepairRequest/Delete/1", new())).StatusCode);
        await app.WithDb(async db => Assert.Equal(RepairRequestStatus.Cancelled,
            (await db.RepairRequests.SingleAsync(r => r.RepairRequestId == 1)).Status));
        Assert.Equal(HttpStatusCode.Conflict, (await app.PostWithToken("/Tenant/RepairRequest/Edit/1", fields)).StatusCode);
    }

    [Fact]
    public async Task Landlord_processed_records_cannot_be_rewritten_by_the_tenant()
    {
        await app.WithDb(async db =>
        {
            (await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 1)).Status = ViewingAppointmentStatus.Completed;
            (await db.RepairRequests.SingleAsync(r => r.RepairRequestId == 1)).Status = RepairRequestStatus.Resolved;
            await db.SaveChangesAsync();
        });
        foreach (var controller in new[] { "ViewingAppointment", "RepairRequest" })
        {
            foreach (var action in new[] { "Edit/1", "Delete/1" })
            {
                Assert.Equal(HttpStatusCode.Conflict, (await app.Client.GetAsync($"/Tenant/{controller}/{action}")).StatusCode);
                Assert.Equal(HttpStatusCode.Conflict, (await app.PostWithToken($"/Tenant/{controller}/{action}", new())).StatusCode);
            }
        }
        await app.WithDb(async db =>
        {
            Assert.Equal(ViewingAppointmentStatus.Completed, (await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 1)).Status);
            Assert.Equal(RepairRequestStatus.Resolved, (await db.RepairRequests.SingleAsync(r => r.RepairRequestId == 1)).Status);
        });
    }

    [Fact]
    public async Task Repair_cannot_be_reassigned_and_active_contract_is_required()
    {
        var fields = new Dictionary<string, string>
        {
            ["RentalPostId"] = "2", ["ProblemType"] = "Water", ["Description"] = "Changed", ["Priority"] = "2"
        };
        Assert.Equal(HttpStatusCode.OK, (await app.PostWithToken("/Tenant/RepairRequest/Edit/1", fields)).StatusCode);
        await app.WithDb(async db =>
        {
            Assert.Equal(1, (await db.RepairRequests.SingleAsync(r => r.RepairRequestId == 1)).RentalPostId);
            (await db.RentalContracts.SingleAsync(c => c.RentalContractId == 1)).Status = ContractStatus.Expired;
            await db.SaveChangesAsync();
        });
        fields["RentalPostId"] = "1";
        Assert.Equal(HttpStatusCode.OK, (await app.PostWithToken("/Tenant/RepairRequest/Create", fields)).StatusCode);
        await app.WithDb(async db => Assert.Equal(2, await db.RepairRequests.CountAsync()));
    }

    [Fact]
    public async Task Invalid_inputs_do_not_save_and_user_text_is_html_encoded()
    {
        var invalid = new Dictionary<string, string>
        {
            ["RentalPostId"] = "1", ["ProblemType"] = "Electricity", ["Description"] = "Broken light", ["Priority"] = "999"
        };
        Assert.Equal(HttpStatusCode.OK, (await app.PostWithToken("/Tenant/RepairRequest/Create", invalid)).StatusCode);
        invalid["Priority"] = "2";
        invalid["Description"] = new string('x', 2001);
        Assert.Equal(HttpStatusCode.OK, (await app.PostWithToken("/Tenant/RepairRequest/Create", invalid)).StatusCode);
        var note = "<script>alert('test')</script>";
        var valid = new Dictionary<string, string>
        {
            ["RentalPostId"] = "3", ["ViewingDate"] = "2027-01-20T10:00", ["Note"] = note
        };
        Assert.Equal(HttpStatusCode.Redirect, (await app.PostWithToken("/Tenant/ViewingAppointment/Edit/1", valid)).StatusCode);
        var html = await (await app.Client.GetAsync("/Tenant/ViewingAppointment/Details/1")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(note, html);
        Assert.Contains("&lt;script&gt;", html);
        await app.WithDb(async db => Assert.Equal(2, await db.RepairRequests.CountAsync()));
    }
}
