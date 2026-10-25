using EZ_Rent.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace TenantSecurity.Tests;

// This convenience belongs only to the isolated preview, never production.
internal static class TenantPreviewEntry
{
    public const string Marker = "tenant-preview-entered";

    public static async Task<bool> TryEnterAsync(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) || request.Cookies.ContainsKey(Marker)) return false;
        if (request.Path.StartsWithSegments("/Identity/Account"))
        {
            // An explicit login/logout must remain explicit on subsequent pages.
            // In particular, logging out a landlord must not auto-enter Tenant.
            context.Response.Cookies.Append(Marker, "1", new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
            return false;
        }
        var tenantEntry = request.Path == "/" || request.Path == "/review/tenant" ||
            request.Path.StartsWithSegments("/Tenant");
        if (!tenantEntry) return false;

        // Never replace a manually signed-in landlord (or another account).
        var session = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        if (session.Succeeded) return false;
        var manager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
        var demo = await manager.FindByIdAsync(TenantTestApp.TenantA);
        if (demo == null) return false;

        await context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>()
            .SignInAsync(demo, isPersistent: false);
        context.Response.Cookies.Append(Marker, "1", new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
        // Establish the cookie before rendering any page/form and its CSRF token.
        context.Response.Redirect(request.PathBase + request.Path + request.QueryString);
        return true;
    }
}
