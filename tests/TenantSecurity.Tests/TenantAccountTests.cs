using System.Net;
using System.Text.RegularExpressions;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace TenantSecurity.Tests;

public sealed class TenantAccountTests : IAsyncLifetime
{
    private readonly TenantTestApp app = new();
    private readonly string uploadRoot = Path.Combine(Path.GetTempPath(), "ez-rent-account-" + Guid.NewGuid().ToString("N"));
    private const string OldPassword = "Original@123";
    private const string NewPassword = "Changed@456";
    private static readonly byte[] Png = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Wl6lJkAAAAASUVORK5CYII=");

    public async Task InitializeAsync()
    {
        await app.InitializeAsync();
        await app.WithServices(async services =>
        {
            services.GetRequiredService<IWebHostEnvironment>().WebRootPath = uploadRoot;
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddPasswordAsync((await users.FindByIdAsync(TenantTestApp.TenantA))!, OldPassword)).Succeeded);
        });
    }

    [Fact]
    public async Task Profile_updates_only_the_signed_in_users_allowed_fields()
    {
        await app.WithDb(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantA)).PhoneNumberConfirmed = true;
            await db.SaveChangesAsync();
        });
        using var response = await app.PostWithToken("/Tenant/Account/UpdateProfile", new()
        {
            ["Profile.FullName"] = "  Dương  ", ["Profile.PhoneNumber"] = "0901234567",
            ["Id"] = TenantTestApp.TenantB, ["Profile.Email"] = "forged@example.test",
            ["Profile.AvatarUrl"] = "/forged.jpg", ["Roles"] = "Admin"
        }, "/Tenant/Account");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        await app.WithDb(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantA);
            Assert.Equal("Dương", user.FullName);
            Assert.Equal("0901234567", user.PhoneNumber);
            Assert.False(user.PhoneNumberConfirmed);
            Assert.Null(user.Email);
            Assert.Null(user.AvatarUrl);
            Assert.Equal("Tenant B", (await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantB)).FullName);
        });
    }

    [Theory]
    [InlineData("   ", "0901234567")]
    [InlineData("Tên hợp lệ", "not-a-phone")]
    public async Task Invalid_profile_does_not_write(string name, string phone)
    {
        using var response = await app.PostWithToken("/Tenant/Account/UpdateProfile", new()
        { ["Profile.FullName"] = name, ["Profile.PhoneNumber"] = phone }, "/Tenant/Account");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("field-validation-error", await response.Content.ReadAsStringAsync());
        await app.WithDb(async db => Assert.Equal("Tenant A", (await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantA)).FullName));
    }

    [Fact]
    public async Task Identity_update_failure_is_shown_and_not_saved()
    {
        // The original model allows this value, but Identity rejects it during UpdateAsync.
        await app.WithDb(async db => { (await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantA)).UserName = "invalid username"; await db.SaveChangesAsync(); });
        using var response = await app.PostWithToken("/Tenant/Account/UpdateProfile", new() { ["Profile.FullName"] = "Must not persist" }, "/Tenant/Account");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("validation-summary-errors", await response.Content.ReadAsStringAsync());
        await app.WithDb(async db => Assert.Equal("Tenant A", (await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantA)).FullName));
    }

    [Theory]
    [InlineData("UpdateProfile")]
    [InlineData("UploadAvatar")]
    [InlineData("RemoveAvatar")]
    [InlineData("UpdatePassword")]
    [InlineData("LinkGoogle")]
    [InlineData("RemoveGoogle")]
    [InlineData("Logout")]
    public async Task Mutations_require_post_and_antiforgery(string action)
    {
        Assert.Equal(HttpStatusCode.MethodNotAllowed, (await app.Client.GetAsync("/Tenant/Account/" + action)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await app.Client.PostAsync("/Tenant/Account/" + action, new FormUrlEncodedContent(new Dictionary<string, string>()))).StatusCode);
    }

    [Fact]
    public async Task Password_rejects_wrong_current_and_policy_then_changes_through_identity()
    {
        foreach (var (current, password) in new[] { ("Wrong@123", NewPassword), (OldPassword, "abcdef") })
        {
            using var invalid = await PasswordPost(current, password);
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            var html = await invalid.Content.ReadAsStringAsync();
            Assert.Contains("field-validation-error", html);
            Assert.DoesNotContain("value=\"" + current, html);
            Assert.DoesNotContain("value=\"" + password, html);
        }
        using var changed = await PasswordPost(OldPassword, NewPassword);
        Assert.Equal(HttpStatusCode.Redirect, changed.StatusCode);
        await app.WithServices(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(TenantTestApp.TenantA))!;
            Assert.True(await users.CheckPasswordAsync(user, NewPassword));
            Assert.False(await users.CheckPasswordAsync(user, OldPassword));
        });
    }

    [Fact]
    public async Task Google_only_account_must_keep_a_login_method_and_can_add_password()
    {
        await app.WithServices(async services =>
        {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(TenantTestApp.TenantA))!;
            Assert.True((await users.RemovePasswordAsync(user)).Succeeded);
            Assert.True((await users.AddLoginAsync(user, new UserLoginInfo("Google", "test-google-key", "Google"))).Succeeded);
        });
        using var blocked = await app.PostWithToken("/Tenant/Account/RemoveGoogle", new(), "/Tenant/Account/Security");
        await AssertGoogleLinked(true);
        using var added = await PasswordPost("", NewPassword);
        Assert.Equal(HttpStatusCode.Redirect, added.StatusCode);
        using var removed = await app.PostWithToken("/Tenant/Account/RemoveGoogle", new(), "/Tenant/Account/Security");
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        await AssertGoogleLinked(false);
    }

    [Fact]
    public async Task Google_is_not_faked_when_unconfigured_or_callback_has_no_external_identity()
    {
        using var link = await app.PostWithToken("/Tenant/Account/LinkGoogle", new(), "/Tenant/Account/Security");
        Assert.Equal(HttpStatusCode.BadRequest, link.StatusCode);
        using var callback = await app.Client.GetAsync("/Tenant/Account/GoogleCallback");
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        await AssertGoogleLinked(false);
    }

    [Fact]
    public async Task Avatar_uses_generated_name_and_removal_keeps_other_tenants_file()
    {
        using var uploaded = await Upload(Png, "../../evil.svg");
        Assert.Equal(HttpStatusCode.Redirect, uploaded.StatusCode);
        string? avatar = null;
        await app.WithDb(async db => avatar = (await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantA)).AvatarUrl);
        Assert.Matches(@"^/uploads/avatars/[a-f0-9]{32}\.png$", avatar!);
        var stored = Path.Combine(uploadRoot, avatar!.TrimStart('/'));
        Assert.True(File.Exists(stored));
        var other = await TenantAvatarStorage.SaveAsync(FormFile(Png, "other.png"), uploadRoot, default);
        await app.WithDb(async db => { (await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantB)).AvatarUrl = other; await db.SaveChangesAsync(); });
        using var removed = await app.PostWithToken("/Tenant/Account/RemoveAvatar", new() { ["AvatarUrl"] = other }, "/Tenant/Account");
        Assert.Equal(HttpStatusCode.Redirect, removed.StatusCode);
        Assert.False(File.Exists(stored));
        Assert.True(File.Exists(Path.Combine(uploadRoot, other.TrimStart('/'))));
    }

    [Fact]
    public async Task Avatar_rejects_svg_and_cleans_new_file_if_identity_save_fails()
    {
        using var bad = await Upload("<svg xmlns='http://www.w3.org/2000/svg'/>"u8.ToArray(), "image.png");
        Assert.Equal(HttpStatusCode.OK, bad.StatusCode);
        Assert.False(Directory.Exists(Path.Combine(uploadRoot, "uploads", "avatars")));
        await app.WithDb(async db => { (await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantA)).UserName = "invalid username"; await db.SaveChangesAsync(); });
        using var failed = await Upload(Png, "valid.png");
        Assert.Equal(HttpStatusCode.OK, failed.StatusCode);
        Assert.Empty(Directory.GetFiles(Path.Combine(uploadRoot, "uploads", "avatars")));
        await app.WithDb(async db => Assert.Null((await db.Users.SingleAsync(u => u.Id == TenantTestApp.TenantA)).AvatarUrl));
    }

    [Fact]
    public async Task Avatar_storage_rejects_oversize_and_invalid_dimensions_and_ignores_external_delete()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => TenantAvatarStorage.SaveAsync(FormFile(new byte[TenantAvatarStorage.MaxBytes + 1], "big.png"), uploadRoot, default));
        var huge = Png.ToArray(); huge[16] = 1;
        await Assert.ThrowsAsync<InvalidDataException>(() => TenantAvatarStorage.SaveAsync(FormFile(huge, "huge.png"), uploadRoot, default));
        var valid = await TenantAvatarStorage.SaveAsync(FormFile(Png, "image.png"), uploadRoot, default);
        TenantAvatarStorage.DeleteLocal("https://example.test" + valid, uploadRoot);
        TenantAvatarStorage.DeleteLocal("/uploads/avatars/../../outside.png", uploadRoot);
        Assert.True(File.Exists(Path.Combine(uploadRoot, valid.TrimStart('/'))));
    }

    [Fact]
    public async Task Avatar_storage_accepts_a_real_jpeg_and_rejects_truncated_bytes()
    {
        byte[] jpeg = [];
        await app.WithServices(services =>
        {
            var root = services.GetRequiredService<IWebHostEnvironment>().ContentRootPath;
            jpeg = File.ReadAllBytes(Path.Combine(root, "wwwroot", "images", "tenant-preview", "living.jpg"));
            return Task.CompletedTask;
        });
        var url = await TenantAvatarStorage.SaveAsync(FormFile(jpeg, "photo.jpg"), uploadRoot, default);
        Assert.EndsWith(".jpg", url);
        Assert.Equal(jpeg, await File.ReadAllBytesAsync(Path.Combine(uploadRoot, url.TrimStart('/'))));
        await Assert.ThrowsAsync<InvalidDataException>(() => TenantAvatarStorage.SaveAsync(FormFile(jpeg[..100], "broken.jpg"), uploadRoot, default));
    }

    private Task<HttpResponseMessage> PasswordPost(string current, string password) => app.PostWithToken("/Tenant/Account/UpdatePassword", new()
    { ["Password.CurrentPassword"] = current, ["Password.NewPassword"] = password, ["Password.ConfirmPassword"] = password }, "/Tenant/Account/Security");

    private async Task AssertGoogleLinked(bool expected) => await app.WithServices(async services =>
    {
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.Equal(expected, (await users.GetLoginsAsync((await users.FindByIdAsync(TenantTestApp.TenantA))!)).Any(l => l.LoginProvider == "Google"));
    });

    private async Task<HttpResponseMessage> Upload(byte[] bytes, string name)
    {
        using var page = await app.Client.GetAsync("/Tenant/Account");
        var html = await page.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        using var request = new HttpRequestMessage(HttpMethod.Post, "/Tenant/Account/UploadAvatar");
        var form = new MultipartFormDataContent { { new StringContent(WebUtility.HtmlDecode(token)), "__RequestVerificationToken" }, { new ByteArrayContent(bytes), "Avatar.File", name } };
        request.Content = form;
        request.Headers.Add("Cookie", string.Join("; ", page.Headers.GetValues("Set-Cookie").Select(v => v.Split(';')[0])));
        return await app.Client.SendAsync(request);
    }

    private static IFormFile FormFile(byte[] bytes, string name) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Avatar.File", name);

    public async Task DisposeAsync()
    {
        await app.DisposeAsync();
        if (Directory.Exists(uploadRoot)) Directory.Delete(uploadRoot, true);
    }
}
