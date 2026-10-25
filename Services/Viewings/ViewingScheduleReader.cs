using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using EZ_Rent.Services.Common;

namespace EZ_Rent.Services.Viewings;

/// <summary>
/// Nguồn khung giờ thật cho trang đặt lịch của Tenant (cài đặt interface ITenantViewingScheduleReader mà Tenant đã chuẩn bị).
/// Quy tắc theo data contract của nhóm:
/// - Chỉ hiện giờ chủ trọ đã mở cho ngày đó; chưa mở → Unopened (không tự sinh giờ mẫu).
/// - Khóa theo người dẫn (chính chủ trọ) + thời điểm, trên mọi phòng của chủ trọ:
///   lịch Confirmed → Booked; lịch Pending còn trong 2 giờ giữ chỗ → Held (của chính người xem → HeldByMe).
/// - Giới hạn 6 giờ báo trước do trang Tenant lọc khi hiển thị (và phải kiểm tra lại ở server khi gửi).
/// Mọi lỗi được bắt và trả State = Error để trang Tenant hiển thị thông báo thay vì lỗi 500.
/// Đăng ký trong LandlordHostingStartup. File này phụ thuộc nhánh Tenant (interface nằm ở Areas/Tenant).
/// </summary>
public sealed class ViewingScheduleReader(AppDbContext db, ILogger<ViewingScheduleReader> logger, TimeProvider? clock = null)
    : ITenantViewingScheduleReader
{
    public static readonly TimeSpan HoldWindow = TimeSpan.FromHours(2);

    public async Task<TenantViewingSchedule> ReadAsync(int postId, string tenantId, DateOnly vietnamDate, CancellationToken cancellationToken)
    {
        try
        {
            var owner = await db.RentalPosts.AsNoTracking()
                .Where(p => p.RentalPostId == postId && p.ApprovalStatus == ApprovalStatus.Approved && p.RoomStatus == RoomStatus.Available)
                .Select(p => p.OwnerId).FirstOrDefaultAsync(cancellationToken);
            if (owner == null) return new TenantViewingSchedule(TenantScheduleState.Unopened, []);
            var hours = await ViewingHoursStore.ReadAsync(db, owner, cancellationToken);
            var times = hours?.TimesFor(vietnamDate) ?? [];
            if (times.Count == 0) return new TenantViewingSchedule(TenantScheduleState.Unopened, []);

            var start = vietnamDate.ToDateTime(TimeOnly.MinValue);
            var end = start.AddDays(1);
            var taken = await db.ViewingAppointments.AsNoTracking()
                .Where(a => a.RentalPost.OwnerId == owner && a.ViewingDate >= start && a.ViewingDate < end &&
                    (a.Status == ViewingAppointmentStatus.Pending || a.Status == ViewingAppointmentStatus.Confirmed))
                .Select(a => new { a.ViewingDate, a.Status, a.TenantId, a.CreatedAt })
                .ToListAsync(cancellationToken);
            var nowUtc = (clock ?? TimeProvider.System).GetUtcNow().UtcDateTime;

            var slots = times.Select(t =>
            {
                var local = vietnamDate.ToDateTime(t);
                var here = taken.Where(a => a.ViewingDate == local).ToList();
                bool Holding(DateTime createdUtc) => nowUtc - DateTime.SpecifyKind(createdUtc, DateTimeKind.Utc) < HoldWindow;
                var state = here.Any(a => a.Status == ViewingAppointmentStatus.Confirmed) ? TenantSlotState.Booked
                    : here.Any(a => a.Status == ViewingAppointmentStatus.Pending && a.TenantId == tenantId && Holding(a.CreatedAt)) ? TenantSlotState.HeldByMe
                    : here.Any(a => a.Status == ViewingAppointmentStatus.Pending && Holding(a.CreatedAt)) ? TenantSlotState.Held
                    : TenantSlotState.Available;
                return new TenantViewingSlot($"{postId}-{local:yyyyMMddHHmm}", new DateTimeOffset(local, VietnamTime.VietnamOffset), state);
            }).ToList();
            return new TenantViewingSchedule(TenantScheduleState.Open, slots);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không đọc được khung giờ xem phòng cho tin {PostId}", postId);
            return new TenantViewingSchedule(TenantScheduleState.Error, []);
        }
    }
}
