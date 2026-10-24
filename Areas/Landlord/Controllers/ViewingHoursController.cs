using System.Globalization;
using EZ_Rent.Areas.Landlord.Helpers;
using EZ_Rent.Services.Billing;
using EZ_Rent.Services.Common;
using EZ_Rent.Services.Notifications;
using EZ_Rent.Services.Payments;
using EZ_Rent.Services.Uploads;
using EZ_Rent.Services.Viewings;
using EZ_Rent.Areas.Landlord.ViewModels.Account;
using EZ_Rent.Areas.Landlord.ViewModels.Contracts;
using EZ_Rent.Areas.Landlord.ViewModels.Invoices;
using EZ_Rent.Areas.Landlord.ViewModels.Meters;
using EZ_Rent.Areas.Landlord.ViewModels.Overview;
using EZ_Rent.Areas.Landlord.ViewModels.Payments;
using EZ_Rent.Areas.Landlord.ViewModels.Repairs;
using EZ_Rent.Areas.Landlord.ViewModels.Rooms;
using EZ_Rent.Areas.Landlord.ViewModels.Shared;
using EZ_Rent.Areas.Landlord.ViewModels.Support;
using EZ_Rent.Areas.Landlord.ViewModels.Viewings;
using EZ_Rent.Data;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// Khung giờ nhận khách xem phòng (phía chủ trọ của luồng đặt lịch theo khung giờ, P03).
/// Trang đặt lịch của Tenant đọc đúng dữ liệu này qua ViewingScheduleReader.
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class ViewingHoursController(AppDbContext db, TimeProvider? clock = null) : LandlordAreaController(db, clock)
{
    private static readonly int[] DayOrder = [1, 2, 3, 4, 5, 6, 0];

    [HttpGet]
    public async Task<IActionResult> Index() => View("Index", await BuildAsync(null, null));

    [HttpPost]
    public async Task<IActionResult> Weekly(ViewingHoursInput input)
    {
        var hours = await ViewingHoursStore.ReadAsync(Db, CurrentUserId) ?? new ViewingHours();
        foreach (var day in DayOrder)
        {
            var (times, error) = ViewingHours.ParseList(input.Weekly.GetValueOrDefault(day));
            if (error != null) ModelState.AddModelError($"Input.Weekly[{day}]", $"{DayName(day)}: {error}");
            else hours.Weekly[day] = times;
        }
        if (!ModelState.IsValid)
        {
            if (IsAjax) return Fail(StatusCodes.Status400BadRequest, string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)), Back(nameof(Index)));
            return View("Index", await BuildAsync(input, null));
        }
        hours.PruneBefore(DateOnly.FromDateTime(VietnamToday));
        await ViewingHoursStore.SaveAsync(Db, CurrentUserId, hours);
        return Done("Đã lưu khung giờ hằng tuần. Người thuê thấy các giờ này khi đặt lịch xem phòng.", Back(nameof(Index)));
    }

    [HttpPost]
    public async Task<IActionResult> AddException([Bind(Prefix = "Exception")] ViewingExceptionInput input)
    {
        var today = DateOnly.FromDateTime(VietnamToday);
        var date = input.Date.HasValue ? DateOnly.FromDateTime(input.Date.Value) : (DateOnly?)null;
        if (date.HasValue && (date < today || date > today.AddDays(180)))
            ModelState.AddModelError("Exception.Date", "Chọn ngày từ hôm nay đến 6 tháng tới.");
        var times = new List<string>();
        if (input.Mode == "custom")
        {
            var (parsed, error) = ViewingHours.ParseList(input.Times);
            if (error != null) ModelState.AddModelError("Exception.Times", error);
            else if (parsed.Count == 0) ModelState.AddModelError("Exception.Times", "Nhập ít nhất một giờ, hoặc chọn “Nghỉ cả ngày”.");
            times = parsed;
        }
        if (!ModelState.IsValid || date == null) return View("Index", await BuildAsync(null, input));
        var hours = await ViewingHoursStore.ReadAsync(Db, CurrentUserId) ?? new ViewingHours();
        hours.PruneBefore(today);
        if (hours.Dates.Count >= ViewingHours.MaxExceptions && !hours.IsException(date.Value))
            return Fail(StatusCodes.Status400BadRequest, $"Tối đa {ViewingHours.MaxExceptions} ngày ngoại lệ.", Back(nameof(Index)));
        hours.Dates[date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)] = times;
        await ViewingHoursStore.SaveAsync(Db, CurrentUserId, hours);
        var pending = await PendingOnAsync(date.Value, times);
        return Done((times.Count == 0 ? $"Đã đặt nghỉ ngày {date.Value:dd/MM/yyyy}." : $"Đã đặt giờ riêng cho ngày {date.Value:dd/MM/yyyy}.") +
            (pending > 0 ? $" Lưu ý: còn {pending} lịch chờ/đã xác nhận ngoài các giờ mới, hãy xử lý trong mục Lịch xem phòng." : ""), Back(nameof(Index)));
    }

    [HttpPost]
    public async Task<IActionResult> RemoveException([FromForm] string date)
    {
        var hours = await ViewingHoursStore.ReadAsync(Db, CurrentUserId);
        if (hours == null || !hours.Dates.Remove(date)) return Fail(StatusCodes.Status404NotFound, "Không tìm thấy ngày ngoại lệ.", Back(nameof(Index)));
        await ViewingHoursStore.SaveAsync(Db, CurrentUserId, hours);
        return Done("Đã bỏ ngoại lệ; ngày đó dùng lại lịch hằng tuần.", Back(nameof(Index)));
    }

    // ===================== Hỗ trợ =====================

    private async Task<ViewingHoursModel> BuildAsync(ViewingHoursInput? posted, ViewingExceptionInput? exception)
    {
        var today = DateOnly.FromDateTime(VietnamToday);
        var hours = await ViewingHoursStore.ReadAsync(Db, CurrentUserId) ?? new ViewingHours();
        var input = posted ?? new ViewingHoursInput
        {
            Weekly = DayOrder.ToDictionary(d => d, d => (string?)string.Join(", ", hours.Weekly.GetValueOrDefault(d) ?? []))
        };
        // Xem trước 7 ngày tới với trạng thái từng giờ, dùng đúng quy tắc của trang đặt lịch phía Tenant.
        var start = today.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(7);
        var appointments = await MyViewings.AsNoTracking()
            .Where(a => a.ViewingDate >= start && a.ViewingDate < end &&
                (a.Status == ViewingAppointmentStatus.Pending || a.Status == ViewingAppointmentStatus.Confirmed))
            .Select(a => new { a.ViewingDate, a.Status, a.CreatedAt }).ToListAsync();
        var minArrival = VietnamNow.AddHours(6);
        var week = Enumerable.Range(0, 7).Select(i => today.AddDays(i)).Select(d => new DayPreview(d, hours.IsException(d),
            hours.TimesFor(d).Select(t =>
            {
                var local = d.ToDateTime(t);
                var here = appointments.Where(a => a.ViewingDate == local).ToList();
                var state = here.Any(a => a.Status == ViewingAppointmentStatus.Confirmed) ? "booked"
                    : here.Any(a => a.Status == ViewingAppointmentStatus.Pending &&
                        UtcNow - DateTime.SpecifyKind(a.CreatedAt, DateTimeKind.Utc) < ViewingScheduleReader.HoldWindow) ? "held"
                    : local < minArrival ? "soon" : "open";
                return new DaySlotPreview(t, state);
            }).ToList())).ToList();
        return new ViewingHoursModel
        {
            Input = input, Exception = exception ?? new ViewingExceptionInput { Date = VietnamToday.AddDays(1) },
            Exceptions = hours.Dates
                .Select(kv => DateOnly.TryParseExact(kv.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)
                    ? (d, (IReadOnlyList<string>)kv.Value) : (DateOnly.MinValue, (IReadOnlyList<string>)kv.Value))
                .Where(x => x.Item1 >= today).OrderBy(x => x.Item1).ToList(),
            Week = week, Configured = hours.HasAnyTime, Today = today
        };
    }

    private async Task<int> PendingOnAsync(DateOnly date, List<string> times)
    {
        var start = date.ToDateTime(TimeOnly.MinValue);
        var end = start.AddDays(1);
        var list = await MyViewings.Where(a => a.ViewingDate >= start && a.ViewingDate < end &&
                (a.Status == ViewingAppointmentStatus.Pending || a.Status == ViewingAppointmentStatus.Confirmed))
            .Select(a => a.ViewingDate).ToListAsync();
        return list.Count(v => !times.Contains(v.ToString("HH:mm", CultureInfo.InvariantCulture)));
    }

    public static string DayName(int day) => day switch
    {
        0 => "Chủ nhật", 1 => "Thứ 2", 2 => "Thứ 3", 3 => "Thứ 4", 4 => "Thứ 5", 5 => "Thứ 6", _ => "Thứ 7"
    };
}
