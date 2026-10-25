using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantPreviewLoginTests
{
    [Theory]
    [InlineData("/Tenant/Dashboard/Messages")]
    [InlineData("/Landlord/Messages")]
    public async Task Direct_login_does_not_silently_sign_in_tenant_and_landlord_credentials_work(string target)
    {
        await using var session = new Session();
        await session.App.InitializeAsync();
        var url = "/Identity/Account/Login?ReturnUrl=" + Uri.EscapeDataString(target);
        using var page = await session.Get(url);
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Contains("id=\"Input_Email\"", await page.Content.ReadAsStringAsync());
        Assert.DoesNotContain(page.Headers.GetValues("Set-Cookie"), value => value.StartsWith(".AspNetCore.Identity.Application="));
        using var login = await session.Login(url);
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(target, login.Headers.Location!.OriginalString);
        using var landlord = await session.Get("/Landlord/Messages");
        Assert.Equal(HttpStatusCode.OK, landlord.StatusCode);
    }

    [Fact]
    public async Task Fresh_landlord_entry_challenges_login_instead_of_becoming_a_tenant()
    {
        await using var session = new Session();
        await session.App.InitializeAsync();
        using var page = await session.Get("/Landlord/Messages");
        Assert.Equal(HttpStatusCode.Redirect, page.StatusCode);
        Assert.Contains("/Identity/Account/Login", page.Headers.Location!.OriginalString);
        Assert.False(page.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Tenant_entry_establishes_cookie_before_forms_and_logout_stays_effective()
    {
        await using var session = new Session();
        await session.App.InitializeAsync();
        using var entry = await session.Get("/Tenant/Account");
        Assert.Equal(HttpStatusCode.Redirect, entry.StatusCode);
        Assert.Equal("/Tenant/Account", entry.Headers.Location!.OriginalString);
        using var profile = await session.Post("/Tenant/Account/UpdateProfile", new()
        {
            ["Profile.FullName"] = "Preview tenant", ["Profile.PhoneNumber"] = "0901234567"
        }, "/Tenant/Account");
        Assert.Equal(HttpStatusCode.Redirect, profile.StatusCode);
        using var logout = await session.Post("/Tenant/Account/Logout", new(), "/Tenant/Account/Security");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var afterLogout = await session.Get("/Tenant/Account");
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.Contains("/Identity/Account/Login", afterLogout.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Manually_signed_in_landlord_is_never_overwritten_by_preview_entry()
    {
        await using var session = new Session();
        await session.App.InitializeAsync();
        using var login = await session.Login("/Identity/Account/Login?ReturnUrl=%2FLandlord%2FMessages");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var wrongArea = await session.Get("/Tenant/Account");
        Assert.Equal(HttpStatusCode.Redirect, wrongArea.StatusCode);
        Assert.Contains("/Identity/Account/AccessDenied", wrongArea.Headers.Location!.OriginalString);
        using var correctArea = await session.Get("/Landlord/Messages");
        Assert.Equal(HttpStatusCode.OK, correctArea.StatusCode);
    }

    [Fact]
    public async Task Manual_landlord_logout_does_not_silently_sign_in_tenant_on_the_next_visit()
    {
        await using var session = new Session();
        await session.App.InitializeAsync();
        using var login = await session.Login("/Identity/Account/Login?ReturnUrl=%2FLandlord%2FMessages");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var logout = await session.Post("/Identity/Account/Logout", new(), "/Landlord/Messages");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        using var tenant = await session.Get("/Tenant/Account");
        Assert.Equal(HttpStatusCode.Redirect, tenant.StatusCode);
        Assert.Contains("/Identity/Account/Login", tenant.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Anonymous_login_post_still_requires_csrf_and_never_bootstraps_a_tenant()
    {
        await using var session = new Session();
        await session.App.InitializeAsync();
        using var response = await session.Send(new(HttpMethod.Post, "/Identity/Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            { ["Input.Email"] = "chutro@example.test", ["Input.Password"] = "LandlordDemo@123" })
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(response.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Stale_login_form_after_account_change_is_rejected_without_switching_the_account()
    {
        await using var session = new Session();
        await session.App.InitializeAsync();
        var token = await session.Token("/Identity/Account/Login");
        using var login = await session.Login("/Identity/Account/Login?ReturnUrl=%2FLandlord%2FMessages");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        using var stale = await session.Send(new(HttpMethod.Post, "/Identity/Account/Login")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Input.Email"] = "minhanh@example.test", ["Input.Password"] = "TenantDemo@123",
                ["__RequestVerificationToken"] = token
            })
        });
        Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
        using var landlord = await session.Get("/Landlord/Messages");
        Assert.Equal(HttpStatusCode.OK, landlord.StatusCode);
    }

    private sealed class Session : IAsyncDisposable
    {
        public TenantTestApp App { get; } = new() { UseIdentityCookies = true, UsePreviewEntry = true };
        private readonly CookieContainer cookies = new();
        private readonly Uri origin = new("http://localhost");

        public Task<HttpResponseMessage> Get(string url) => Send(new(HttpMethod.Get, url));

        public async Task<string> Token(string url)
        {
            using var page = await Get(url);
            Assert.Equal(HttpStatusCode.OK, page.StatusCode);
            var html = await page.Content.ReadAsStringAsync();
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(token.Success);
            return WebUtility.HtmlDecode(token.Groups[1].Value);
        }

        public Task<HttpResponseMessage> Login(string url) => Post(url, new()
        { ["Input.Email"] = "chutro@example.test", ["Input.Password"] = "LandlordDemo@123" }, url);

        public async Task<HttpResponseMessage> Post(string url, Dictionary<string, string> fields, string tokenPage)
        {
            fields["__RequestVerificationToken"] = await Token(tokenPage);
            return await Send(new(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) });
        }

        public async Task<HttpResponseMessage> Send(HttpRequestMessage request)
        {
            using (request)
            {
                var cookie = cookies.GetCookieHeader(origin);
                if (cookie.Length > 0) request.Headers.Add("Cookie", cookie);
                var response = await App.Client.SendAsync(request);
                if (response.Headers.TryGetValues("Set-Cookie", out var values))
                    foreach (var value in values) cookies.SetCookies(origin, value);
                return response;
            }
        }

        public async ValueTask DisposeAsync() => await App.DisposeAsync();
    }
}
