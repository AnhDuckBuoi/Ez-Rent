using System.Text.Json;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;
using EZ_Rent.Services.Meters;
using EZ_Rent.Services.Viewings;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Data.Seeder;

public sealed record DatasetDemoInfo(string TenantEmail, string TenantName, string LandlordEmail, string LandlordName,
    int ConversationId, int RoomId, int? ContractId, int? InvoiceId);

// Supplements only an isolated demo database. The original DbSeeder stays unchanged.
public static class DatasetDemoSeed
{
    public static string DatabaseName(string? connectionString)
    {
        var name = new SqlConnectionStringBuilder(connectionString ?? "").InitialCatalog;
        if (!name.StartsWith("EZRent_Demo_", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Môi trường Demo chỉ dùng database riêng có tên bắt đầu bằng EZRent_Demo_. Xem scripts/Start-Demo.ps1.");
        return name;
    }

    public static async Task<DatasetDemoInfo> PrepareAsync(IServiceProvider services, IWebHostEnvironment environment)
    {
        if (environment.EnvironmentName != "Demo") throw new InvalidOperationException("Chỉ bổ sung dataset trong môi trường Demo.");
        var db = services.GetRequiredService<AppDbContext>();
        var database = DatabaseName(db.Database.GetConnectionString());
        var conversation = await db.Conversations.AsNoTracking().Include(c => c.Initiator).Include(c => c.Recipient)
            .Where(c => c.ConversationType == ConversationType.TenantLandlord && c.RentalPostId != null &&
                c.Initiator.Email != null && c.Initiator.Email.StartsWith("tenant") &&
                c.Recipient.Email != null && c.Recipient.Email.StartsWith("landlord"))
            .OrderByDescending(c => db.RentalContracts.Any(r => r.TenantId == c.InitiatorId && r.LandlordId == c.RecipientId && r.Status == ContractStatus.Active))
            .ThenBy(c => c.ConversationId).FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Không tìm thấy hội thoại từ DbSeeder. Hãy dùng một database demo mới, không xóa database hiện tại.");
        var tenantId = conversation.InitiatorId;
        var landlordId = conversation.RecipientId;
        var roomId = await db.RentalPosts.Where(p => p.OwnerId == landlordId && p.ApprovalStatus == ApprovalStatus.Approved && p.RoomStatus == RoomStatus.Available)
            .OrderBy(p => p.RentalPostId).Select(p => (int?)p.RentalPostId).FirstOrDefaultAsync()
            ?? await db.RentalPosts.Where(p => p.ApprovalStatus == ApprovalStatus.Approved && p.RoomStatus == RoomStatus.Available)
                .OrderBy(p => p.RentalPostId).Select(p => p.RentalPostId).FirstAsync();
        var contract = await db.RentalContracts.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.LandlordId == landlordId && c.Status == ContractStatus.Active)
            .OrderBy(c => c.RentalContractId).FirstOrDefaultAsync();
        // Missing from the original dataset. Never overwrite demo edits on restart.
        var landlordIds = await db.Users.Where(u => u.Email != null && u.Email.StartsWith("landlord") && u.Email.EndsWith("@ezrent.vn"))
            .Select(u => u.Id).ToListAsync();
        foreach (var owner in landlordIds)
            if (await ViewingHoursStore.ReadAsync(db, owner) == null)
                await ViewingHoursStore.SaveAsync(db, owner, new ViewingHours
                {
                    Weekly = Enumerable.Range(0, 7).ToDictionary(day => day, _ => new List<string> { "09:00", "13:00", "17:00" })
                });
        var documents = services.GetRequiredService<RentalDocumentService>();
        if (contract != null && contract.CurrentDocumentVersion == 0)
        {
            await using var template = File.OpenRead(Path.Combine(environment.ContentRootPath, "docs", "templates", "bm03-hop-dong-thue-phong.pdf"));
            await documents.AppendContractAsync(contract.RentalContractId, landlordId, 0,
                FormFile(template, "BM03-mau-minh-hoa-chua-ky.pdf", "application/pdf"));
        }
        var invoice = contract == null ? null : await db.Invoices.Include(i => i.MeterReading)
            .Where(i => i.RentalContractId == contract.RentalContractId && i.MeterReadingId != null)
            .OrderBy(i => i.InvoiceId).FirstOrDefaultAsync();
        if (invoice?.MeterReading is { } reading && !await db.InvoiceMeterEvidence.AnyAsync(e => e.InvoiceId == invoice.InvoiceId))
        {
            await using var electric = File.OpenRead(Path.Combine(environment.ContentRootPath, "docs", "demo-assets", "electric-meter.png"));
            await using var water = File.OpenRead(Path.Combine(environment.ContentRootPath, "docs", "demo-assets", "water-meter.png"));
            var store = services.GetRequiredService<RentalPrivateFileStore>();
            reading.ElectricMeterImageUrl = await MeterPhotoFiles.SaveAsync(store, FormFile(electric, "dien-minh-hoa.png", "image/png"), default);
            reading.WaterMeterImageUrl = await MeterPhotoFiles.SaveAsync(store, FormFile(water, "nuoc-minh-hoa.png", "image/png"), default);
            await db.SaveChangesAsync();
            electric.Position = 0; water.Position = 0;
            await documents.CaptureInvoiceAsync(invoice.InvoiceId, landlordId,
                FormFile(electric, "dien-minh-hoa.png", "image/png"), FormFile(water, "nuoc-minh-hoa.png", "image/png"));
        }
        var info = new DatasetDemoInfo(conversation.Initiator.Email!, conversation.Initiator.FullName,
            conversation.Recipient.Email!, conversation.Recipient.FullName, conversation.ConversationId, roomId,
            contract?.RentalContractId, invoice?.InvoiceId);
        var folder = Path.Combine(environment.ContentRootPath, "App_Data", "Demo", database);
        Directory.CreateDirectory(folder);
        await File.WriteAllTextAsync(Path.Combine(folder, "demo-links.json"), JsonSerializer.Serialize(info, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"DATASET DEMO: Tenant {info.TenantEmail} / Tenant@123; Landlord {info.LandlordEmail} / Landlord@123; Conversation {info.ConversationId}");
        return info;
    }

    private static IFormFile FormFile(FileStream stream, string name, string contentType) => new FormFile(stream, 0, stream.Length, name, name)
    { Headers = new HeaderDictionary(), ContentType = contentType };
}
