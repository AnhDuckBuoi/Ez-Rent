using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace TenantSecurity.Tests;

// Unlike the ownership tests, this exercises the real Identity application cookie and login page.
public sealed class TenantAccountCookieTests
{
    [Fact]
    public async Task Login_profile_password_refresh_and_logout_use_real_identity_sessions()
    {
        await using var fixture = new Fixture();
        await fixture.App.InitializeAsync();
        var anonymous = await fixture.Get("/Tenant/Account");
        Assert.Equal(HttpStatusCode.Redirect, anonymous.StatusCode);
        Assert.StartsWith("http://localhost/Identity/Account/Login", anonymous.Headers.Location!.ToString());

        var login = await fixture.Post("/Identity/Account/Login?returnUrl=%2FTenant%2FAccount", new()
        { ["Input.Email"] = "minhanh@example.test", ["Input.Password"] = "TenantDemo@123" }, "/Identity/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Get("/Tenant/Account")).StatusCode);

        var profile = await fixture.Post("/Tenant/Account/UpdateProfile", new()
        { ["Profile.FullName"] = "Cookie Tenant", ["Profile.PhoneNumber"] = "0901234567" }, "/Tenant/Account");
        Assert.Equal(HttpStatusCode.Redirect, profile.StatusCode);
        Assert.Contains("Cookie Tenant", await (await fixture.Get("/Tenant/Account")).Content.ReadAsStringAsync());

        var password = await fixture.Post("/Tenant/Account/UpdatePassword", new()
        { ["Password.CurrentPassword"] = "TenantDemo@123", ["Password.NewPassword"] = "ChangedDemo@456", ["Password.ConfirmPassword"] = "ChangedDemo@456" }, "/Tenant/Account/Security");
        Assert.Equal(HttpStatusCode.Redirect, password.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Get("/Tenant/Account/Security")).StatusCode);

        var logout = await fixture.Post("/Tenant/Account/Logout", new(), "/Tenant/Account/Security");
        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await fixture.Get("/Tenant/Account")).StatusCode);
        var relogin = await fixture.Post("/Identity/Account/Login?returnUrl=%2FTenant%2FAccount", new()
        { ["Input.Email"] = "minhanh@example.test", ["Input.Password"] = "ChangedDemo@456" }, "/Identity/Account/Login");
        Assert.Equal(HttpStatusCode.Redirect, relogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await fixture.Get("/Tenant/Account")).StatusCode);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public TenantTestApp App { get; } = new() { UseIdentityCookies = true };
        private readonly CookieContainer cookies = new();
        private readonly Uri origin = new("http://localhost");

        public Task<HttpResponseMessage> Get(string url) => Send(new(HttpMethod.Get, url));

        public async Task<HttpResponseMessage> Post(string url, Dictionary<string, string> fields, string tokenPage)
        {
            using var page = await Get(tokenPage);
            page.EnsureSuccessStatusCode();
            var html = await page.Content.ReadAsStringAsync();
            var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
            Assert.True(token.Success);
            fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
            return await Send(new(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(fields) });
        }

        private async Task<HttpResponseMessage> Send(HttpRequestMessage request)
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
