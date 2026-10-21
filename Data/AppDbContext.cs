using EZ_Rent.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<RentalPost> RentalPosts => Set<RentalPost>();
    public DbSet<RoomImage> RoomImages => Set<RoomImage>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<RentalRequest> RentalRequests => Set<RentalRequest>();
    public DbSet<ViewingAppointment> ViewingAppointments => Set<ViewingAppointment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<Message> Messages => Set<Message>();
    public DbSet<RentalContract> RentalContracts => Set<RentalContract>();
    public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<RepairRequest> RepairRequests => Set<RepairRequest>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<SearchHistory> SearchHistories => Set<SearchHistory>();

    // ============================================================
    // TỰ ĐỘNG CẬP NHẬT CreatedAt / UpdatedAt
    // ============================================================
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override int SaveChanges()
    {
        ApplyTimestamps();
        return base.SaveChanges();
    }

    private void ApplyTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            // ===== KHI TẠO MỚI =====
            if (entry.State == EntityState.Added)
            {
                var createdProp = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "CreatedAt");

                if (createdProp != null && createdProp.CurrentValue == null)
                    createdProp.CurrentValue = DateTime.UtcNow;
            }

            // ===== KHI SỬA =====
            if (entry.State == EntityState.Modified)
            {
                var updatedProp = entry.Properties
                    .FirstOrDefault(p => p.Metadata.Name == "UpdatedAt");

                if (updatedProp != null)
                    updatedProp.CurrentValue = DateTime.UtcNow;
            }
        }
    }
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // ===== Decimal precision =====
        foreach (var entityType in builder.Model.GetEntityTypes())
        {
            foreach (var prop in entityType.GetProperties())
            {
                if (prop.ClrType == typeof(decimal) || prop.ClrType == typeof(decimal?))
                {
                    prop.SetColumnType("decimal(18,2)");
                }
            }
        }

        // ===== Favorite =====
        builder.Entity<Favorite>()
            .HasIndex(f => new { f.UserId, f.RentalPostId })
            .IsUnique();

        builder.Entity<Favorite>()
            .HasOne(f => f.User)
            .WithMany(u => u.Favorites)
            .HasForeignKey(f => f.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Favorite>()
            .HasOne(f => f.RentalPost)
            .WithMany(p => p.Favorites)
            .HasForeignKey(f => f.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== RentalPost =====
        builder.Entity<RentalPost>()
            .HasOne(p => p.Owner)
            .WithMany(u => u.RentalPosts)
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RentalPost>()
            .HasOne(p => p.ApprovedBy)
            .WithMany()
            .HasForeignKey(p => p.ApprovedById)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<RentalPost>()
            .HasIndex(p => new { p.City, p.District, p.Price });

        builder.Entity<RentalPost>()
            .HasIndex(p => new { p.ApprovalStatus, p.RoomStatus });

        // ===== RentalRequest =====
        builder.Entity<RentalRequest>()
            .HasOne(r => r.Tenant)
            .WithMany(u => u.RentalRequests)
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RentalRequest>()
            .HasOne(r => r.RentalPost)
            .WithMany(p => p.RentalRequests)
            .HasForeignKey(r => r.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== ViewingAppointment =====
        builder.Entity<ViewingAppointment>()
            .HasOne(v => v.Tenant)
            .WithMany(u => u.ViewingAppointments)
            .HasForeignKey(v => v.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<ViewingAppointment>()
            .HasOne(v => v.RentalPost)
            .WithMany(p => p.ViewingAppointments)
            .HasForeignKey(v => v.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== Conversation =====
        builder.Entity<Conversation>()
            .HasOne(c => c.Initiator)
            .WithMany(u => u.ConversationsInitiated)
            .HasForeignKey(c => c.InitiatorId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Conversation>()
            .HasOne(c => c.Recipient)
            .WithMany(u => u.ConversationsReceived)
            .HasForeignKey(c => c.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Conversation>()
            .HasOne(c => c.RentalPost)
            .WithMany(p => p.Conversations)
            .HasForeignKey(c => c.RentalPostId)
            .OnDelete(DeleteBehavior.SetNull);

        // ===== Message =====
        builder.Entity<Message>()
            .HasOne(m => m.Conversation)
            .WithMany(c => c.Messages)
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Message>()
            .HasOne(m => m.Sender)
            .WithMany(u => u.MessagesSent)
            .HasForeignKey(m => m.SenderId)
            .OnDelete(DeleteBehavior.Restrict);

        // ===== RentalContract =====
        builder.Entity<RentalContract>()
            .HasOne(c => c.Tenant)
            .WithMany(u => u.ContractsAsTenant)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RentalContract>()
            .HasOne(c => c.Landlord)
            .WithMany(u => u.ContractsAsLandlord)
            .HasForeignKey(c => c.LandlordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RentalContract>()
            .HasOne(c => c.RentalPost)
            .WithMany(p => p.RentalContracts)
            .HasForeignKey(c => c.RentalPostId)
            .OnDelete(DeleteBehavior.Restrict);

        // Mỗi phòng chỉ có tối đa 1 hợp đồng Active
        builder.Entity<RentalContract>()
            .HasIndex(c => c.RentalPostId)
            .HasFilter("[Status] = 4")
            .IsUnique();

        // ===== MeterReading =====
        builder.Entity<MeterReading>()
            .HasIndex(m => new { m.RentalPostId, m.Period })
            .IsUnique();

        builder.Entity<MeterReading>()
            .HasOne(m => m.RecordedBy)
            .WithMany(u => u.MeterReadings)
            .HasForeignKey(m => m.RecordedById)
            .OnDelete(DeleteBehavior.Restrict);

        // ===== Invoice =====
        builder.Entity<Invoice>()
            .HasOne(i => i.RentalContract)
            .WithMany(c => c.Invoices)
            .HasForeignKey(i => i.RentalContractId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<Invoice>()
            .HasOne(i => i.MeterReading)
            .WithMany(m => m.Invoices)
            .HasForeignKey(i => i.MeterReadingId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.Entity<Invoice>()
            .HasIndex(i => new { i.RentalContractId, i.BillingPeriod })
            .IsUnique();

        // ===== RepairRequest =====
        builder.Entity<RepairRequest>()
            .HasOne(r => r.Tenant)
            .WithMany(u => u.RepairRequests)
            .HasForeignKey(r => r.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Entity<RepairRequest>()
            .HasOne(r => r.RentalPost)
            .WithMany(p => p.RepairRequests)
            .HasForeignKey(r => r.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== Service =====
        builder.Entity<Service>()
            .HasOne(s => s.RentalPost)
            .WithMany(p => p.Services)
            .HasForeignKey(s => s.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== RoomImage =====
        builder.Entity<RoomImage>()
            .HasOne(r => r.RentalPost)
            .WithMany(p => p.RoomImages)
            .HasForeignKey(r => r.RentalPostId)
            .OnDelete(DeleteBehavior.Cascade);

        // ===== Notification =====
        builder.Entity<Notification>()
            .HasOne(n => n.User)
            .WithMany()
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        // ===== SearchHistory =====
        builder.Entity<SearchHistory>()
            .HasOne(s => s.User)
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<SearchHistory>()
            .HasIndex(s => new { s.UserId, s.SearchedAt });
    }
}