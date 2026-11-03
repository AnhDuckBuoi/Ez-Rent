using System.Net;
using System.Text.RegularExpressions;
using EZ_Rent.Areas.Identity.Pages.Account;
using EZ_Rent.Areas.Tenant.Controllers;
using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class RegistrationRoleTests
{
    [Theory]
    [InlineData(null, "Tenant")]
    [InlineData("", "Tenant")]
    [InlineData("   ", "Tenant")]
    [InlineData("Tenant", "Tenant")]
    [InlineData("tenant", "Tenant")]
    [InlineData("Landlord", "Landlord")]
    [InlineData("landlord", "Landlord")]
    public void Allowed_self_register_roles_normalize_to_canonical_role(string? raw, string expected)
    {
        var ok = RegisterModel.TryNormalizeSelfRegisterRole(raw, out var normalized);
        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("admin")]
    [InlineData("Administrator")]
    [InlineData("SuperUser")]
    public void Privileged_or_unknown_roles_are_strictly_rejected(string raw)
    {
        var ok = RegisterModel.TryNormalizeSelfRegisterRole(raw, out var normalized);
        Assert.False(ok);
        Assert.Null(normalized);
    }

    [Fact]
    public async Task Register_page_renders_segmented_role_pills_and_assigns_landlord_or_tenant_safely()
    {
        await using var host = await RegistrationHost.StartAsync();

        // 1. Default GET selects Tenant pill
        using var getDefault = await host.Get("/Identity/Account/Register");
        Assert.Equal(HttpStatusCode.OK, getDefault.StatusCode);
        var defaultHtml = await getDefault.Content.ReadAsStringAsync();
        Assert.Contains("ez-role-switch", defaultHtml);
        Assert.Contains("Tôi là Chủ trọ", defaultHtml);
        Assert.Contains("Tôi là Người thuê", defaultHtml);
        Assert.Matches("id=\"role-tenant\"[^>]*checked=\"checked\"", defaultHtml);

        // 2. GET ?role=Landlord pre-selects Landlord pill
        using var getLandlord = await host.Get("/Identity/Account/Register?role=Landlord");
        Assert.Equal(HttpStatusCode.OK, getLandlord.StatusCode);
        var landlordHtml = await getLandlord.Content.ReadAsStringAsync();
        Assert.Matches("id=\"role-landlord\"[^>]*checked=\"checked\"", landlordHtml);

        // 3. POST Role=Landlord creates Landlord account and redirects to /Landlord/Overview
        using var postLandlord = await host.PostRegister(new()
        {
            ["Input.Role"] = "Landlord",
            ["Input.FullName"] = "Chủ Trọ Mới",
            ["Input.Email"] = "newlandlord@example.test",
            ["Input.Password"] = "SafePass@123",
            ["Input.ConfirmPassword"] = "SafePass@123"
        });
        Assert.Equal(HttpStatusCode.Redirect, postLandlord.StatusCode);
        Assert.Equal("/Landlord/Overview", postLandlord.Headers.Location!.OriginalString);
        Assert.Equal(new[] { "Landlord" }, await host.GetRolesAsync("newlandlord@example.test"));

        // 3b. POST Role=Tenant creates Tenant account and redirects to /
        await using var tenantHost = await RegistrationHost.StartAsync();
        using var postTenant = await tenantHost.PostRegister(new()
        {
            ["Input.Role"] = "Tenant",
            ["Input.FullName"] = "Người Thuê Mới",
            ["Input.Email"] = "newtenant@example.test",
            ["Input.Password"] = "SafePass@123",
            ["Input.ConfirmPassword"] = "SafePass@123"
        });
        Assert.Equal(HttpStatusCode.Redirect, postTenant.StatusCode);
        Assert.Equal("/", postTenant.Headers.Location!.OriginalString);
        Assert.Equal(new[] { "Tenant" }, await tenantHost.GetRolesAsync("newtenant@example.test"));

        // 4. POST tampered Role=Admin is rejected and never creates user or Admin role
        await using var attackerHost = await RegistrationHost.StartAsync();
        using var postAdmin = await attackerHost.PostRegister(new()
        {
            ["Input.Role"] = "Admin",
            ["Input.FullName"] = "Attacker",
            ["Input.Email"] = "attacker@example.test",
            ["Input.Password"] = "SafePass@123",
            ["Input.ConfirmPassword"] = "SafePass@123"
        });
        Assert.Equal(HttpStatusCode.OK, postAdmin.StatusCode);
        var rejectedHtml = WebUtility.HtmlDecode(await postAdmin.Content.ReadAsStringAsync());
        Assert.Contains("Vai trò đăng ký không hợp lệ", rejectedHtml);
        Assert.Null(await attackerHost.FindUserAsync("attacker@example.test"));
    }

    private sealed class NoOpEmailSender : IEmailSender
    {
        public Task SendEmailAsync(string email, string subject, string htmlMessage) => Task.CompletedTask;
    }

    private sealed class RegistrationHost : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly CookieContainer _cookies = new();
        private readonly Uri _origin = new("http://localhost");
        private WebApplication _app = null!;
        private HttpClient _client = null!;

        public static async Task<RegistrationHost> StartAsync()
        {
            var host = new RegistrationHost();
            await host._connection.OpenAsync();

            var root = FindProjectRoot();
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                ApplicationName = typeof(FavoriteController).Assembly.GetName().Name,
                EnvironmentName = "Testing",
                ContentRootPath = root
            });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddControllersWithViews().AddApplicationPart(typeof(FavoriteController).Assembly);
            builder.Services.AddRazorPages();
            builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(host._connection));
            builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();
            builder.Services.AddSingleton<IEmailSender, NoOpEmailSender>();

            host._app = builder.Build();
            host._app.UseStaticFiles();
            host._app.UseRouting();
            host._app.UseAuthentication();
            host._app.UseAuthorization();
            host._app.MapRazorPages();

            using (var scope = host._app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.EnsureCreatedAsync();
                var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                foreach (var role in new[] { "Admin", "Landlord", "Tenant" })
                {
                    if (!await roleManager.RoleExistsAsync(role))
                        await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            await host._app.StartAsync();
            host._client = host._app.GetTestClient();
            return host;
        }

        public async Task<HttpResponseMessage> Get(string url)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            var cookie = _cookies.GetCookieHeader(_origin);
            if (cookie.Length > 0) req.Headers.Add("Cookie", cookie);
            var res = await _client.SendAsync(req);
            if (res.Headers.TryGetValues("Set-Cookie", out var values))
                foreach (var v in values) _cookies.SetCookies(_origin, v);
            return res;
        }

        public async Task<HttpResponseMessage> PostRegister(Dictionary<string, string> fields)
        {
            using var get = await Get("/Identity/Account/Register");
            var html = await get.Content.ReadAsStringAsync();
            var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(match.Success);
            fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);

            using var req = new HttpRequestMessage(HttpMethod.Post, "/Identity/Account/Register")
            {
                Content = new FormUrlEncodedContent(fields)
            };
            var cookie = _cookies.GetCookieHeader(_origin);
            if (cookie.Length > 0) req.Headers.Add("Cookie", cookie);
            var res = await _client.SendAsync(req);
            if (res.Headers.TryGetValues("Set-Cookie", out var values))
                foreach (var v in values) _cookies.SetCookies(_origin, v);
            return res;
        }

        public async Task<ApplicationUser?> FindUserAsync(string email)
        {
            using var scope = _app.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            return await userManager.FindByEmailAsync(email);
        }

        public async Task<IList<string>> GetRolesAsync(string email)
        {
            using var scope = _app.Services.CreateScope();
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = await userManager.FindByEmailAsync(email);
            Assert.NotNull(user);
            return await userManager.GetRolesAsync(user!);
        }

        private static string FindProjectRoot()
        {
            for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
                if (File.Exists(Path.Combine(dir.FullName, "EZ-Rent.csproj")))
                    return dir.FullName;
            throw new DirectoryNotFoundException("Could not locate EZ-Rent.csproj.");
        }

        public async ValueTask DisposeAsync()
        {
            _client?.Dispose();
            if (_app != null) await _app.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
