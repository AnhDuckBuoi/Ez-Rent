namespace EZ_Rent.Areas.Tenant.Services;

// Read-only UI seam. Dương will provide the shared schedule/Landlord integration separately.
// Register a real reader with DI when that source is ready; no reader means no invented slots.
public interface ITenantViewingScheduleReader
{
    Task<TenantViewingSchedule> ReadAsync(int postId, string tenantId, DateOnly vietnamDate, CancellationToken cancellationToken);
}

public enum TenantScheduleState { NotConnected, Open, Unopened, Full, Error }
public enum TenantSlotState { Available, Booked, Held, HeldByMe }
public sealed record TenantViewingSlot(string Id, DateTimeOffset ArrivalAt, TenantSlotState State);
public sealed record TenantViewingSchedule(TenantScheduleState State, IReadOnlyList<TenantViewingSlot> Slots, bool IsDemo = false);
