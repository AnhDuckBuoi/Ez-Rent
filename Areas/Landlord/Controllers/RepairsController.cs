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
/// UC24 Xử lý yêu cầu sửa chữa (P08, BM06). Đồng bộ với Tenant: người thuê chỉ sửa/hủy khi còn "Chờ xử lý";
/// chủ trọ tiếp nhận (Đang xử lý), bổ sung ghi chú, hoàn tất (Đã xử lý); mọi lần cập nhật được lưu thành lịch sử. Cập nhật kèm điều kiện trạng thái
/// nên không ghi đè thao tác hủy vừa xảy ra phía người thuê (trả 409).
/// </summary>
public sealed class RepairsController(AppDbContext db, TimeProvider? clock = null) : LandlordControllerBase(db, clock)
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] RepairListInput input)
    {
        if (!ModelState.IsValid) return InvalidQuery(Back(nameof(Index)));
        var mine = MyRepairs.AsNoTracking();
        if (input.Priority.HasValue) mine = mine.Where(r => r.Priority == input.Priority.Value);
        var counts = await mine.GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var query = input.Status.HasValue ? mine.Where(r => r.Status == input.Status.Value) : mine;
        var total = await query.CountAsync();
        var page = PagedList<RepairRow>.ClampPage(input.Page, total, PageSize);
        var rows = await query
            .OrderBy(r => r.Status == RepairRequestStatus.Pending ? 0 : r.Status == RepairRequestStatus.InProgress ? 1 : 2)
            .ThenByDescending(r => r.Priority).ThenBy(r => r.CreatedAt).ThenBy(r => r.RepairRequestId)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(Projection).ToListAsync();
        input.Page = page;
        return PageOrRegion("Index", "_RepairList", new RepairListModel
        {
            Input = input, Counts = counts, Items = new PagedList<RepairRow> { Items = rows, Page = page, PageSize = PageSize, Total = total }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var r = await MyRepairs.AsNoTracking().Where(x => x.RepairRequestId == id)
            .Select(x => new { Row = new RepairRow
                {
                    Id = x.RepairRequestId, RoomId = x.RentalPostId, RoomTitle = x.RentalPost.Title, TenantName = x.Tenant.FullName,
                    ProblemType = x.ProblemType, Description = x.Description, Priority = x.Priority, Status = x.Status,
                    CreatedAt = x.CreatedAt, ResolvedAt = x.ResolvedAt, HasImage = x.ImageUrl != null
                }, x.TenantId, x.Tenant.PhoneNumber, x.ImageUrl, x.ResolutionNote, x.UpdatedAt })
            .FirstOrDefaultAsync();
        if (r == null) return MissingPage("yêu cầu sửa chữa", Back(nameof(Index)));
        return PageOrRegion("Details", "_RepairPanel", new RepairDetailModel
        {
            Item = r.Row, TenantId = r.TenantId, TenantPhone = r.PhoneNumber, ImageUrl = LandlordFormat.SafeMediaUrl(r.ImageUrl),
            ResolutionNote = r.ResolutionNote, UpdatedAt = r.UpdatedAt
        });
    }

    [HttpPost]
    public async Task<IActionResult> Accept([FromRoute] int id, RepairActionInput input)
    {
        var back = Back(nameof(Details), new { id });
        var r = await LoadAsync(id);
        if (r == null) return MissingPage("yêu cầu sửa chữa", Back(nameof(Index)));
        if (!ModelState.IsValid) return Fail(StatusCodes.Status400BadRequest, "Ghi chú tối đa 2000 ký tự.", back);
        var note = NoteOrNull(input.Note, 1000);
        var history = AppendHistory(r.ResolutionNote, "Tiếp nhận" + (note == null ? "" : ": " + note));
        var changed = await Db.RepairRequests.Where(x => x.RepairRequestId == id && x.Status == RepairRequestStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, RepairRequestStatus.InProgress)
                .SetProperty(x => x.ResolutionNote, history).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Yêu cầu không còn ở trạng thái “Chờ xử lý” (người thuê có thể vừa hủy).", back);
        LandlordNotifier.Add(Db, r.TenantId, "Chủ nhà đã tiếp nhận yêu cầu sửa chữa",
            $"{r.ProblemType} tại {r.RentalPost.Title} đang được xử lý." + (note == null ? "" : $" Ghi chú: {note}"),
            LandlordNotifier.RepairLink(id), "repair", UtcNow);
        await Db.SaveChangesAsync();
        return Done("Đã tiếp nhận yêu cầu. Người thuê được báo đang xử lý.", back);
    }

    [HttpPost]
    public async Task<IActionResult> Note([FromRoute] int id, RepairActionInput input)
    {
        var back = Back(nameof(Details), new { id });
        var r = await LoadAsync(id);
        if (r == null) return MissingPage("yêu cầu sửa chữa", Back(nameof(Index)));
        var note = NoteOrNull(input.Note, 1000);
        if (!ModelState.IsValid || note == null) return Fail(StatusCodes.Status400BadRequest, "Nhập nội dung cập nhật (tối đa 1000 ký tự).", back);
        var old = r.ResolutionNote;
        var history = AppendHistory(old, note);
        // Chỉ ghi nếu lịch sử chưa bị ai đổi trong lúc này, tránh mất một dòng cập nhật.
        var changed = await Db.RepairRequests.Where(x => x.RepairRequestId == id && x.Status == RepairRequestStatus.InProgress && x.ResolutionNote == old)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ResolutionNote, history).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Yêu cầu vừa được cập nhật ở nơi khác hoặc không còn đang xử lý. Hãy tải lại trang.", back);
        LandlordNotifier.Add(Db, r.TenantId, "Cập nhật tiến độ sửa chữa", $"{r.ProblemType}: {note}", LandlordNotifier.RepairLink(id), "repair", UtcNow);
        await Db.SaveChangesAsync();
        return Done("Đã cập nhật tiến độ cho người thuê.", back);
    }

    [HttpPost]
    public async Task<IActionResult> Resolve([FromRoute] int id, RepairActionInput input)
    {
        var back = Back(nameof(Details), new { id });
        var r = await LoadAsync(id);
        if (r == null) return MissingPage("yêu cầu sửa chữa", Back(nameof(Index)));
        var note = NoteOrNull(input.Note, 1000);
        if (!ModelState.IsValid || note == null) return Fail(StatusCodes.Status400BadRequest, "Ghi lại nội dung đã xử lý để người thuê và bạn tra cứu sau này.", back);
        var history = AppendHistory(r.ResolutionNote, "Đã xử lý: " + note);
        var changed = await Db.RepairRequests.Where(x => x.RepairRequestId == id &&
                (x.Status == RepairRequestStatus.Pending || x.Status == RepairRequestStatus.InProgress))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, RepairRequestStatus.Resolved)
                .SetProperty(x => x.ResolutionNote, history).SetProperty(x => x.ResolvedAt, UtcNow).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Yêu cầu đã được xử lý hoặc đã bị hủy.", back);
        LandlordNotifier.Add(Db, r.TenantId, "Sự cố đã được xử lý xong", $"{r.ProblemType} tại {r.RentalPost.Title}: {note}",
            LandlordNotifier.RepairLink(id), "repair", UtcNow);
        await Db.SaveChangesAsync();
        return Done("Đã đánh dấu đã xử lý và báo cho người thuê.", back);
    }

    /// <summary>
    /// Lịch sử xử lý (UC24, R43, Hình 3.5 "Xem lịch sử xử lý yêu cầu"): mỗi lần cập nhật thêm một dòng
    /// "[dd/MM/yyyy HH:mm] nội dung" vào ResolutionNote thay vì ghi đè. Người thuê đọc cùng trường này nên thấy đủ tiến trình.
    /// Trường tối đa 2000 ký tự: khi đầy thì bỏ các dòng cũ nhất.
    /// </summary>
    private string AppendHistory(string? existing, string entry)
    {
        var line = $"[{LandlordTime.Full(VietnamNow)}] {entry.Replace('\r', ' ').Replace('\n', ' ')}";
        var lines = (existing ?? "").Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList();
        lines.Add(line);
        while (lines.Count > 1 && string.Join('\n', lines).Length > 2000) lines.RemoveAt(0);
        var result = string.Join('\n', lines);
        return result.Length <= 2000 ? result : result[^2000..];
    }

    private Task<RepairRequest?> LoadAsync(int id) =>
        MyRepairs.AsNoTracking().Include(r => r.RentalPost).FirstOrDefaultAsync(r => r.RepairRequestId == id);

    private static readonly System.Linq.Expressions.Expression<Func<RepairRequest, RepairRow>> Projection = r => new RepairRow
    {
        Id = r.RepairRequestId, RoomId = r.RentalPostId, RoomTitle = r.RentalPost.Title, TenantName = r.Tenant.FullName,
        ProblemType = r.ProblemType, Description = r.Description, Priority = r.Priority, Status = r.Status,
        CreatedAt = r.CreatedAt, ResolvedAt = r.ResolvedAt, HasImage = r.ImageUrl != null
    };
}
