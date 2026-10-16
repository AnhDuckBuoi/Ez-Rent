using System.Text.RegularExpressions;
using EZ_Rent.Data;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Tenant.Services;

// A notification link is untrusted data, even when it came from our own database.
public sealed record TenantNotificationTarget(string Controller, int Id)
{
    public static TenantNotificationTarget? Parse(string? link)
    {
        if (string.IsNullOrWhiteSpace(link) || link.Length > 512) return null;
        var match = Regex.Match(link, @"\A/Tenant/(ViewingAppointment|RentalPost|Contract|Invoice|RepairRequest)/Details/([1-9][0-9]*)/?\z",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
        return match.Success && int.TryParse(match.Groups[2].Value, out var id)
            ? new(match.Groups[1].Value, id) : null;
    }

    public Task<bool> CanAccessAsync(AppDbContext db, string userId) => Controller switch
    {
        "ViewingAppointment" => db.ViewingAppointments.AnyAsync(a => a.ViewingAppointmentId == Id && a.TenantId == userId),
        "RentalPost" => db.RentalPosts.VisibleToTenant().AnyAsync(p => p.RentalPostId == Id),
        "Contract" => db.RentalContracts.AnyAsync(c => c.RentalContractId == Id && c.TenantId == userId),
        "Invoice" => db.Invoices.AnyAsync(i => i.InvoiceId == Id && i.RentalContract.TenantId == userId),
        "RepairRequest" => db.RepairRequests.AnyAsync(r => r.RepairRequestId == Id && r.TenantId == userId),
        _ => Task.FromResult(false)
    };
}
