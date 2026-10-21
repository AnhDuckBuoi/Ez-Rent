using EZ_Rent.Areas.Landlord.Services;
using EZ_Rent.Areas.Landlord.ViewModels;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC08 Xử lý lịch xem phòng (P03, BM02). Đồng bộ với Tenant:
/// - Cùng 5 trạng thái và cùng nhãn; không xóa bản ghi, chỉ đổi trạng thái để giữ lịch sử hai bên.
/// - Mọi cập nhật kèm điều kiện trạng thái trong câu UPDATE: nếu người thuê vừa hủy/sửa thì trả 409, không ghi đè.
/// - Người dẫn xem là chính chủ trọ: không xác nhận hai lịch cùng một giờ đến (khóa theo người dẫn + thời điểm).
/// - Xác nhận/từ chối/hoàn thành tạo thông báo cho người thuê với link /Tenant/ViewingAppointment/Details/{id}.
/// </summary>
public sealed class ViewingsController(AppDbContext db, TimeProvider? clock = null) : LandlordControllerBase(db, clock)
{
    private static readonly TimeSpan ResponseWindow = TimeSpan.FromHours(2);

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ViewingListInput input)
    {
        if (!ModelState.IsValid) return InvalidQuery(Back(nameof(Index)));
        var mine = MyViewings.AsNoTracking();
        if (input.Room.HasValue) mine = mine.Where(a => a.RentalPostId == input.Room.Value);
        var counts = await mine.GroupBy(a => a.Status).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        var query = input.Status.HasValue ? mine.Where(a => a.Status == input.Status.Value) : mine;
        var total = await query.CountAsync();
        var page = PagedList<ViewingRow>.ClampPage(input.Page, total, PageSize);
        // Giống thứ tự phía Tenant: lịch đang chờ/đã xác nhận theo giờ tăng dần, sau đó lịch sử mới nhất trước.
        var raw = await query
            .OrderBy(a => a.Status == ViewingAppointmentStatus.Pending || a.Status == ViewingAppointmentStatus.Confirmed ? 0 : 1)
            .ThenBy(a => a.Status == ViewingAppointmentStatus.Pending || a.Status == ViewingAppointmentStatus.Confirmed ? a.ViewingDate : DateTime.MaxValue)
            .ThenByDescending(a => a.ViewingDate).ThenByDescending(a => a.ViewingAppointmentId)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(a => new { a.ViewingAppointmentId, a.RentalPostId, a.RentalPost.Title, TenantName = a.Tenant.FullName,
                a.ViewingDate, a.Status, a.Note, a.LandlordNote, a.CreatedAt })
            .ToListAsync();
        var rooms = await MyPosts.AsNoTracking().Where(p => p.ViewingAppointments.Any())
            .OrderBy(p => p.Title).Select(p => new { p.RentalPostId, p.Title }).ToListAsync();
        input.Page = page;
        var model = new ViewingListModel
        {
            Input = input, Counts = counts,
            Rooms = rooms.Select(r => (r.RentalPostId, r.Title)).ToList(),
            Items = new PagedList<ViewingRow>
            {
                Page = page, PageSize = PageSize, Total = total,
                Items = raw.Select(a => Row(a.ViewingAppointmentId, a.RentalPostId, a.Title, a.TenantName, a.ViewingDate,
                    a.Status, a.Note, a.LandlordNote, a.CreatedAt)).ToList()
            }
        };
        return PageOrRegion("Index", "_ViewingList", model);
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var a = await MyViewings.AsNoTracking().Where(x => x.ViewingAppointmentId == id)
            .Select(x => new
            {
                x.ViewingAppointmentId, x.RentalPostId, x.RentalPost.Title, x.RentalPost.Address, x.RentalPost.District, x.RentalPost.City,
                x.TenantId, TenantName = x.Tenant.FullName, x.Tenant.PhoneNumber, x.Tenant.Email,
                x.ViewingDate, x.Status, x.Note, x.LandlordNote, x.CreatedAt, x.UpdatedAt
            }).FirstOrDefaultAsync();
        if (a == null) return MissingPage("lịch hẹn", Back(nameof(Index)));
        var sameTime = await MyViewings.AsNoTracking()
            .Where(x => x.ViewingAppointmentId != id && x.ViewingDate == a.ViewingDate &&
                (x.Status == ViewingAppointmentStatus.Pending || x.Status == ViewingAppointmentStatus.Confirmed))
            .Select(x => new { x.ViewingAppointmentId, x.RentalPostId, x.RentalPost.Title, TenantName = x.Tenant.FullName,
                x.ViewingDate, x.Status, x.Note, x.LandlordNote, x.CreatedAt })
            .ToListAsync();
        // Chỉ hiện số điện thoại/email khi lịch còn hiệu lực, tránh lộ thông tin liên hệ của lịch đã hủy/từ chối.
        var active = a.Status is ViewingAppointmentStatus.Pending or ViewingAppointmentStatus.Confirmed;
        var model = new ViewingDetailModel
        {
            Item = Row(a.ViewingAppointmentId, a.RentalPostId, a.Title, a.TenantName, a.ViewingDate, a.Status, a.Note, a.LandlordNote, a.CreatedAt),
            TenantId = a.TenantId, TenantPhone = active ? a.PhoneNumber : null, TenantEmail = active ? a.Email : null,
            RoomAddress = string.Join(", ", new[] { a.Address, a.District, a.City }.Where(x => !string.IsNullOrWhiteSpace(x))),
            UpdatedAt = a.UpdatedAt,
            SameTime = sameTime.Select(x => Row(x.ViewingAppointmentId, x.RentalPostId, x.Title, x.TenantName, x.ViewingDate,
                x.Status, x.Note, x.LandlordNote, x.CreatedAt)).ToList()
        };
        return PageOrRegion("Details", "_ViewingPanel", model);
    }

    [HttpPost]
    public async Task<IActionResult> Confirm([FromRoute] int id, ViewingActionInput input)
    {
        var back = Back(nameof(Details), new { id });
        var a = await LoadAsync(id);
        if (a == null) return MissingPage("lịch hẹn", Back(nameof(Index)));
        if (!ModelState.IsValid) return Fail(StatusCodes.Status400BadRequest, "Ghi chú tối đa 500 ký tự.", back);
        if (a.Status != ViewingAppointmentStatus.Pending)
            return Fail(StatusCodes.Status409Conflict, $"Lịch này đang ở trạng thái “{LandlordLabels.Viewing(a.Status).Text}”, không thể xác nhận.", back);
        if (a.ViewingDate <= VietnamNow)
            return Fail(StatusCodes.Status409Conflict, "Giờ hẹn đã qua. Hãy từ chối kèm lời nhắn để người thuê chọn giờ khác.", back);

        var note = NoteOrNull(input.Note, 500);
        await using var tx = await Db.Database.BeginTransactionAsync();
        if (await MyViewings.AnyAsync(x => x.ViewingAppointmentId != id && x.ViewingDate == a.ViewingDate && x.Status == ViewingAppointmentStatus.Confirmed))
            return Fail(StatusCodes.Status409Conflict, $"Bạn đã xác nhận một lịch khác lúc {LandlordTime.Full(a.ViewingDate)}. Một người dẫn không nhận hai lịch cùng giờ đến.", back);
        var changed = await Db.ViewingAppointments
            .Where(x => x.ViewingAppointmentId == id && x.Status == ViewingAppointmentStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ViewingAppointmentStatus.Confirmed)
                .SetProperty(x => x.LandlordNote, x => note ?? x.LandlordNote).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Người thuê vừa thay đổi lịch này. Hãy tải lại trang.", back);
        LandlordNotifier.Add(Db, a.TenantId, "Lịch xem phòng đã được xác nhận",
            $"{a.RentalPost.Title}: {LandlordTime.LongDate(a.ViewingDate)} lúc {LandlordTime.Time(a.ViewingDate)}." + (note == null ? "" : $" Lời nhắn: {note}"),
            LandlordNotifier.ViewingLink(id), "viewing", UtcNow);
        await Db.SaveChangesAsync();
        await tx.CommitAsync();
        return Done("Đã xác nhận lịch xem phòng và báo cho người thuê.", back);
    }

    [HttpPost]
    public async Task<IActionResult> Reject([FromRoute] int id, ViewingActionInput input)
    {
        var back = Back(nameof(Details), new { id });
        var a = await LoadAsync(id);
        if (a == null) return MissingPage("lịch hẹn", Back(nameof(Index)));
        var note = NoteOrNull(input.Note, 500);
        if (!ModelState.IsValid || note == null)
            return Fail(StatusCodes.Status400BadRequest, "Nhập lý do từ chối (tối đa 500 ký tự) để người thuê biết cách chọn lịch khác.", back);
        if (a.Status != ViewingAppointmentStatus.Pending)
            return Fail(StatusCodes.Status409Conflict, $"Lịch này đang ở trạng thái “{LandlordLabels.Viewing(a.Status).Text}”, không thể từ chối.", back);
        var changed = await Db.ViewingAppointments
            .Where(x => x.ViewingAppointmentId == id && x.Status == ViewingAppointmentStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ViewingAppointmentStatus.Rejected)
                .SetProperty(x => x.LandlordNote, note).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Người thuê vừa thay đổi lịch này. Hãy tải lại trang.", back);
        LandlordNotifier.Add(Db, a.TenantId, "Chủ nhà đã từ chối yêu cầu xem phòng",
            $"{a.RentalPost.Title} lúc {LandlordTime.Full(a.ViewingDate)}. Lý do: {note}", LandlordNotifier.ViewingLink(id), "viewing", UtcNow);
        await Db.SaveChangesAsync();
        return Done("Đã từ chối yêu cầu và gửi lý do cho người thuê.", back);
    }

    [HttpPost]
    public async Task<IActionResult> Complete([FromRoute] int id, ViewingActionInput input)
    {
        var back = Back(nameof(Details), new { id });
        var a = await LoadAsync(id);
        if (a == null) return MissingPage("lịch hẹn", Back(nameof(Index)));
        if (!ModelState.IsValid) return Fail(StatusCodes.Status400BadRequest, "Ghi chú tối đa 500 ký tự.", back);
        if (a.Status != ViewingAppointmentStatus.Confirmed)
            return Fail(StatusCodes.Status409Conflict, "Chỉ lịch đã xác nhận mới được cập nhật “Đã hoàn thành”.", back);
        if (a.ViewingDate > VietnamNow)
            return Fail(StatusCodes.Status409Conflict, "Buổi xem phòng chưa diễn ra. Cập nhật “Đã hoàn thành” sau giờ hẹn.", back);
        var note = NoteOrNull(input.Note, 500);
        var changed = await Db.ViewingAppointments
            .Where(x => x.ViewingAppointmentId == id && x.Status == ViewingAppointmentStatus.Confirmed)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ViewingAppointmentStatus.Completed)
                .SetProperty(x => x.LandlordNote, x => note ?? x.LandlordNote).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Người thuê vừa hủy lịch này. Hãy tải lại trang.", back);
        LandlordNotifier.Add(Db, a.TenantId, "Buổi xem phòng đã hoàn thành",
            $"Cảm ơn bạn đã đến xem {a.RentalPost.Title}. Bạn có thể nhắn tin cho chủ nhà nếu muốn thuê.",
            LandlordNotifier.ViewingLink(id), "viewing", UtcNow);
        await Db.SaveChangesAsync();
        return Done("Đã cập nhật buổi xem phòng: Đã hoàn thành.", back);
    }

    private Task<ViewingAppointment?> LoadAsync(int id) =>
        MyViewings.AsNoTracking().Include(a => a.RentalPost).FirstOrDefaultAsync(a => a.ViewingAppointmentId == id);

    private ViewingRow Row(int id, int roomId, string title, string tenant, DateTime date, ViewingAppointmentStatus status,
        string? note, string? landlordNote, DateTime createdAt) => new()
    {
        Id = id, RoomId = roomId, RoomTitle = title, TenantName = string.IsNullOrWhiteSpace(tenant) ? "Người thuê" : tenant,
        ViewingDate = date, Status = status, Note = note, LandlordNote = landlordNote, CreatedAt = createdAt,
        IsPast = date <= VietnamNow,
        ResponseLate = status == ViewingAppointmentStatus.Pending && UtcNow - DateTime.SpecifyKind(createdAt, DateTimeKind.Utc) > ResponseWindow
    };
}
