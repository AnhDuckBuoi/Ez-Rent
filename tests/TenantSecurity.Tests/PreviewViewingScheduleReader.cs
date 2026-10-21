using EZ_Rent.Areas.Tenant.Services;

namespace TenantSecurity.Tests;

// Registered only by the independent --preview executable. Never registered in Program.cs.
public sealed class PreviewViewingScheduleReader : ITenantViewingScheduleReader
{
    public Task<TenantViewingSchedule> ReadAsync(int postId, string tenantId, DateOnly vietnamDate, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)).DateTime);
        var index = (vietnamDate.DayNumber - today.DayNumber) % 7;
        var state = index switch { 2 => TenantScheduleState.Full, 3 => TenantScheduleState.Unopened, 4 => TenantScheduleState.Error, _ => TenantScheduleState.Open };
        var slots = new[] { 9, 11, 13, 14, 15, 16, 17, 18 }.Select(h => new TenantViewingSlot(
            $"demo-{postId}-{vietnamDate:yyyyMMdd}-{h}", new DateTimeOffset(vietnamDate.ToDateTime(new TimeOnly(h, 0)), TimeSpan.FromHours(7)),
            h == 15 || state == TenantScheduleState.Full ? TenantSlotState.Booked : h == 17 ? TenantSlotState.Held : TenantSlotState.Available)).ToList();
        return Task.FromResult(new TenantViewingSchedule(state, slots, IsDemo: true));
    }
}
