using System.Globalization;
using System.Text.Json;
using EZ_Rent.Areas.Landlord.Helpers;
using EZ_Rent.Services.Billing;
using EZ_Rent.Services.Common;
using EZ_Rent.Services.Notifications;
using EZ_Rent.Services.Payments;
using EZ_Rent.Services.Uploads;
using EZ_Rent.Services.Rooms;
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
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Areas.Landlord.Controllers;

/// <summary>
/// UC03 Đăng tin phòng trọ, UC04 Quản lý thông tin phòng (quy trình P02, biểu mẫu BM01): danh sách, đăng, sửa, ẩn/hiện, gửi duyệt, xóa tin.
/// Ảnh/video của tin nằm ở RoomMediaController.
/// </summary>
[Area("Landlord")]
[Authorize(Roles = "Landlord")]
public sealed class RoomsController(AppDbContext db, IWebHostEnvironment env, TimeProvider? clock = null)
    : LandlordAreaController(db, clock)
{

    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index([FromQuery] RoomListInput input)
    {
        if (!ModelState.IsValid) return InvalidQuery(Back(nameof(Index)));
        var counts = await MyPosts.AsNoTracking().GroupBy(p => p.ApprovalStatus)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);

        var query = MyPosts.AsNoTracking();
        if (input.Approval.HasValue) query = query.Where(p => p.ApprovalStatus == input.Approval.Value);
        if (input.Room.HasValue) query = query.Where(p => p.RoomStatus == input.Room.Value);
        var keyword = input.Q?.Trim();
        if (!string.IsNullOrEmpty(keyword))
            query = query.Where(p => p.Title.Contains(keyword) || p.Address.Contains(keyword) || p.District.Contains(keyword));

        var rooms = await PageAsync(query
            .OrderByDescending(p => p.UpdatedAt ?? p.CreatedAt).ThenByDescending(p => p.RentalPostId)
            .Select(p => new RoomRow
            {
                Id = p.RentalPostId, Title = p.Title,
                Address = p.Address + ", " + p.District + ", " + p.City,
                Price = p.Price, Area = p.Area, RoomType = p.RoomType,
                Approval = p.ApprovalStatus, Room = p.RoomStatus, ApprovalNote = p.ApprovalNote,
                ImageUrl = p.RoomImages.Where(i => i.MediaType == MediaType.Image)
                    .OrderByDescending(i => i.IsPrimary).ThenBy(i => i.DisplayOrder).ThenBy(i => i.RoomImageId)
                    .Select(i => i.MediaUrl).FirstOrDefault(),
                PendingViewings = p.ViewingAppointments.Count(a => a.Status == ViewingAppointmentStatus.Pending),
                Favorites = p.Favorites.Count(),
                TenantName = p.RentalContracts.Where(c => c.Status == ContractStatus.Active).Select(c => c.Tenant.FullName).FirstOrDefault(),
                UpdatedAt = p.UpdatedAt ?? p.CreatedAt
            }), input.Page);
        input.Page = rooms.PageIndex;
        var model = new RoomListModel { Input = input, Counts = counts, Rooms = rooms, Pager = Pager(rooms) };
        return PageOrRegion("Index", "_RoomList", model);
    }

    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var model = await LoadDetailAsync(id);
        if (model == null) return NotFoundPage("tin phòng", Back(nameof(Index)));
        return PageOrRegion("Details", "_Media", model);
    }

    [HttpGet]
    [ValidateAntiForgeryToken]
    public IActionResult Create() => View("Create", new RoomFormModel
    {
        Input = new RoomInput { AvailableFrom = VietnamToday, Services = [] }
    });

    [HttpPost]
    [RequestSizeLimit(160 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 160 * 1024 * 1024)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] RoomInput input, [FromForm] string? intent)
    {
        var area = ValidateRoom(input);
        var uploads = new List<CheckedUpload>();
        if (input.Media.Count > RoomMediaWriter.MaxMediaPerRoom) ModelState.AddModelError("Input.Media", $"Tối đa {RoomMediaWriter.MaxMediaPerRoom} ảnh/video mỗi tin.");
        foreach (var file in input.Media.Where(f => f.Length > 0).Take(RoomMediaWriter.MaxMediaPerRoom))
        {
            var (upload, error) = await UploadStorage.CheckAsync(file, allowVideo: true, null, HttpContext.RequestAborted);
            if (error != null) ModelState.AddModelError("Input.Media", error); else uploads.Add(upload!);
        }
        if (!ModelState.IsValid) return View("Create", new RoomFormModel { Input = input });

        var submit = intent == "submit";
        var post = new RentalPost
        {
            OwnerId = CurrentUserId,
            ApprovalStatus = submit ? ApprovalStatus.Pending : ApprovalStatus.Draft,
            RoomStatus = RoomStatus.Available,
            CreatedAt = UtcNow
        };
        Apply(post, input, area);
        foreach (var service in CleanServices(input)) post.Services.Add(service);
        Db.RentalPosts.Add(post);
        await Db.SaveChangesAsync();

        var saved = await RoomMediaWriter.AddAsync(Db, env, post.RentalPostId, uploads, startOrder: 0, hasPrimary: false, UtcNow, HttpContext.RequestAborted);
        await Db.SaveChangesAsync();

        var message = submit
            ? "Đã gửi tin cho quản trị viên kiểm duyệt. Tin sẽ hiển thị với người thuê sau khi được duyệt."
            : "Đã lưu bản nháp. Bạn có thể bổ sung ảnh và gửi duyệt khi sẵn sàng.";
        if (saved < uploads.Count) message += $" Có {uploads.Count - saved} file không lưu được, hãy tải lại.";
        return Done(message, Back(nameof(Details), new { id = post.RentalPostId }));
    }

    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id)
    {
        var post = await MyPosts.AsNoTracking().Include(p => p.Services).FirstOrDefaultAsync(p => p.RentalPostId == id);
        if (post == null) return NotFoundPage("tin phòng", Back(nameof(Index)));
        return View("Edit", new RoomFormModel
        {
            Id = id, CurrentApproval = post.ApprovalStatus, ApprovalNote = post.ApprovalNote,
            Input = new RoomInput
            {
                Title = post.Title, Description = post.Description, RoomType = post.RoomType, Price = post.Price,
                Area = post.Area.ToString("0.##", CultureInfo.InvariantCulture), Address = post.Address, Ward = post.Ward,
                District = post.District, City = post.City, MapLocation = post.MapLocation,
                Amenities = ReadAmenities(post.AmenitiesJson), ElectricityUnitPrice = post.ElectricityUnitPrice,
                WaterUnitPrice = post.WaterUnitPrice, ServiceFee = post.ServiceFee, AvailableFrom = post.AvailableFrom.Date,
                InternetFee = post.Services.FirstOrDefault(IsInternet)?.UnitPrice,
                Services = post.Services.Where(s => !IsInternet(s)).OrderBy(s => s.ServiceId)
                    .Select(s => new ServiceInput { Name = s.Name, UnitPrice = s.UnitPrice, Unit = s.Unit }).ToList()
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit([FromRoute] int id, [Bind(Prefix = "Input")] RoomInput input, [FromForm] string? intent)
    {
        var post = await MyPosts.Include(p => p.Services).FirstOrDefaultAsync(p => p.RentalPostId == id);
        if (post == null) return NotFoundPage("tin phòng", Back(nameof(Index)));
        input.Media = [];
        ModelState.Remove("Input.Media");
        var area = ValidateRoom(input);
        if (!ModelState.IsValid)
            return View("Edit", new RoomFormModel { Id = id, Input = input, CurrentApproval = post.ApprovalStatus, ApprovalNote = post.ApprovalNote });

        Apply(post, input, area);
        Db.Services.RemoveRange(post.Services);
        foreach (var service in CleanServices(input)) post.Services.Add(service);

        // P02: nội dung đã thay đổi thì tin phải qua kiểm duyệt lại trước khi hiển thị cho người thuê.
        string message;
        if (post.ApprovalStatus == ApprovalStatus.Draft && intent != "submit")
        {
            message = "Đã lưu bản nháp.";
        }
        else if (post.RoomStatus == RoomStatus.Rented && post.ApprovalStatus == ApprovalStatus.Approved)
        {
            // Phòng đang cho thuê không hiện trên trang tìm phòng nên chưa cần duyệt ngay. Đánh dấu ApprovedAt = null:
            // khi kết thúc hợp đồng, tin tự chuyển "Chờ duyệt" (ContractsController.End). Cách này cũng tránh việc
            // nút Duyệt của Admin (luôn đặt RoomStatus = Available) làm phòng đang có hợp đồng hiện lại là "Còn trống".
            post.ApprovedAt = null;
            message = "Đã lưu. Phòng đang cho thuê nên tin sẽ được gửi duyệt lại khi hợp đồng kết thúc.";
        }
        else
        {
            var wasApproved = post.ApprovalStatus == ApprovalStatus.Approved;
            post.ApprovalStatus = ApprovalStatus.Pending;
            post.ApprovedById = null;
            post.ApprovedAt = null;
            message = wasApproved
                ? "Đã lưu và gửi duyệt lại. Tin tạm ẩn khỏi trang tìm phòng cho đến khi quản trị viên duyệt."
                : "Đã lưu và gửi tin cho quản trị viên kiểm duyệt.";
        }
        await Db.SaveChangesAsync();
        return Done(message, Back(nameof(Details), new { id }));
    }

    /// <summary>Gửi duyệt nhanh một bản nháp hoặc tin cần chỉnh sửa (không đổi nội dung).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit([FromRoute] int id)
    {
        var back = Back(nameof(Details), new { id });
        var post = await MyPosts.AsNoTracking().Include(p => p.RoomImages).FirstOrDefaultAsync(p => p.RentalPostId == id);
        if (post == null) return NotFoundPage("tin phòng", Back(nameof(Index)));
        if (post.ApprovalStatus is not (ApprovalStatus.Draft or ApprovalStatus.NeedRevision or ApprovalStatus.Rejected))
            return Fail(StatusCodes.Status409Conflict, "Tin này đang chờ duyệt hoặc đã được duyệt.", back);
        if (!post.RoomImages.Any(i => i.MediaType == MediaType.Image))
            return Fail(StatusCodes.Status400BadRequest, "Thêm ít nhất 1 ảnh phòng trước khi gửi duyệt.", back);
        var changed = await Db.RentalPosts.Where(p => p.RentalPostId == id && p.OwnerId == CurrentUserId && p.ApprovalStatus == post.ApprovalStatus)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ApprovalStatus, ApprovalStatus.Pending).SetProperty(p => p.UpdatedAt, UtcNow));
        return changed == 0
            ? Fail(StatusCodes.Status409Conflict, "Trạng thái tin vừa thay đổi, hãy tải lại trang.", back)
            : Done("Đã gửi tin cho quản trị viên kiểm duyệt.", back);
    }

    /// <summary>AJAX: ẩn/hiện tin trên trang tìm phòng. Phòng đang cho thuê do hợp đồng quyết định.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Visibility([FromRoute] int id, [FromForm] bool visible)
    {
        var back = Request.Headers.Referer.ToString() is { Length: > 0 } r && Url.IsLocalUrl(r) ? r : Back(nameof(Details), new { id });
        var post = await MyPosts.AsNoTracking().FirstOrDefaultAsync(p => p.RentalPostId == id);
        if (post == null) return NotFoundPage("tin phòng", Back(nameof(Index)));
        if (post.RoomStatus == RoomStatus.Rented)
            return Fail(StatusCodes.Status409Conflict, "Phòng đang có hợp đồng hiệu lực. Kết thúc hợp đồng để mở lại tin.", back);
        var target = visible ? RoomStatus.Available : RoomStatus.Hidden;
        await Db.RentalPosts.Where(p => p.RentalPostId == id && p.OwnerId == CurrentUserId && p.RoomStatus != RoomStatus.Rented)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.RoomStatus, target).SetProperty(p => p.UpdatedAt, UtcNow));
        var note = visible && post.ApprovalStatus != ApprovalStatus.Approved ? " Tin chỉ hiển thị sau khi được duyệt." : "";
        return Done(visible ? "Đã mở lại tin trên trang tìm phòng." + note : "Đã ẩn tin khỏi trang tìm phòng.", back);
    }

    [HttpGet]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete([FromRoute] int id)
    {
        var model = await LoadDetailAsync(id);
        if (model == null) return NotFoundPage("tin phòng", Back(nameof(Index)));
        return View("Delete", model);
    }

    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed([FromRoute] int id)
    {
        var post = await MyPosts.Include(p => p.RoomImages).FirstOrDefaultAsync(p => p.RentalPostId == id);
        if (post == null) return NotFoundPage("tin phòng", Back(nameof(Index)));
        if (!await CanDeleteAsync(id))
            return Fail(StatusCodes.Status409Conflict,
                "Tin đã phát sinh lịch hẹn, tin nhắn, hợp đồng hoặc dữ liệu thuê nên phải giữ lại để tra cứu. Bạn có thể ẩn tin.",
                Back(nameof(Details), new { id }));
        var files = post.RoomImages.Select(i => i.MediaUrl).ToList();
        Db.RentalPosts.Remove(post);
        await Db.SaveChangesAsync();
        foreach (var url in files) UploadStorage.TryDelete(env, url);
        return Done("Đã xóa tin phòng.", Back(nameof(Index)));
    }

    // ===================== Hỗ trợ =====================

    private async Task<RoomDetailModel?> LoadDetailAsync(int id)
    {
        var post = await MyPosts.AsNoTracking().Include(p => p.RoomImages).Include(p => p.Services)
            .AsSplitQuery().FirstOrDefaultAsync(p => p.RentalPostId == id);
        if (post == null) return null;
        var now = VietnamNow;
        var active = await Db.RentalContracts.AsNoTracking()
            .Where(c => c.RentalPostId == id && c.Status == ContractStatus.Active)
            .Select(c => new { c.RentalContractId, c.Tenant.FullName }).FirstOrDefaultAsync();
        return new RoomDetailModel
        {
            Id = post.RentalPostId, Title = post.Title, Description = post.Description, RoomType = post.RoomType,
            Price = post.Price, Area = post.Area, Address = post.Address, Ward = post.Ward, District = post.District,
            City = post.City, MapLocation = post.MapLocation, MapUrl = MapUrl(post.MapLocation),
            Amenities = ReadAmenities(post.AmenitiesJson), ElectricityUnitPrice = post.ElectricityUnitPrice,
            WaterUnitPrice = post.WaterUnitPrice, ServiceFee = post.ServiceFee, AvailableFrom = post.AvailableFrom,
            Approval = post.ApprovalStatus, Room = post.RoomStatus, ApprovalNote = post.ApprovalNote,
            CreatedAt = post.CreatedAt, UpdatedAt = post.UpdatedAt,
            Media = post.RoomImages.OrderBy(i => i.DisplayOrder).ThenBy(i => i.RoomImageId)
                .Select(i => new { i, url = DisplayFormat.SafeMediaUrl(i.MediaUrl) }).Where(x => x.url != null)
                .Select(x => new RoomMediaItem(x.i.RoomImageId, x.url!, x.i.MediaType, x.i.IsPrimary, x.i.DisplayOrder)).ToList(),
            Services = post.Services.OrderBy(s => s.ServiceId).Select(s => new RoomServiceItem(s.Name, s.UnitPrice, s.Unit)).ToList(),
            PendingViewings = await Db.ViewingAppointments.CountAsync(a => a.RentalPostId == id && a.Status == ViewingAppointmentStatus.Pending),
            Favorites = await Db.Favorites.CountAsync(f => f.RentalPostId == id),
            UpcomingViewings = await Db.ViewingAppointments.CountAsync(a => a.RentalPostId == id && a.Status == ViewingAppointmentStatus.Confirmed && a.ViewingDate >= now),
            OpenRepairs = await Db.RepairRequests.CountAsync(r => r.RentalPostId == id && (r.Status == RepairRequestStatus.Pending || r.Status == RepairRequestStatus.InProgress)),
            ActiveContractId = active?.RentalContractId, TenantName = active?.FullName,
            CanDelete = await CanDeleteAsync(id)
        };
    }

    private async Task<bool> CanDeleteAsync(int id) =>
        !await Db.ViewingAppointments.AnyAsync(a => a.RentalPostId == id) &&
        !await Db.RentalContracts.AnyAsync(c => c.RentalPostId == id) &&
        !await Db.Conversations.AnyAsync(c => c.RentalPostId == id) &&
        !await Db.RepairRequests.AnyAsync(r => r.RentalPostId == id) &&
        !await Db.MeterReadings.AnyAsync(m => m.RentalPostId == id) &&
        !await Db.RentalRequests.AnyAsync(r => r.RentalPostId == id);

    private double ValidateRoom(RoomInput input)
    {
        var area = 0d;
        if (!string.IsNullOrWhiteSpace(input.Area) &&
            (!double.TryParse(input.Area.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out area) || area < 5 || area > 1000))
            ModelState.AddModelError("Input.Area", "Diện tích từ 5 đến 1000 m².");
        if (!Enum.IsDefined(input.RoomType)) ModelState.AddModelError("Input.RoomType", "Chọn loại phòng.");
        if (input.AvailableFrom.HasValue && input.AvailableFrom.Value.Date > VietnamToday.AddYears(1))
            ModelState.AddModelError("Input.AvailableFrom", "Ngày vào ở không quá 1 năm kể từ hôm nay.");
        if (!string.IsNullOrWhiteSpace(input.MapLocation) && MapUrl(input.MapLocation) == null)
            ModelState.AddModelError("Input.MapLocation", "Dán link Google Maps (https://...) hoặc tọa độ dạng 21.0285, 105.8542.");
        input.Amenities = input.Amenities.Where(LandlordLabels.Amenities.Contains).Distinct().ToList();
        if (input.Services.Count > 10) ModelState.AddModelError("Input.Services", "Tối đa 10 dịch vụ.");
        for (var i = 0; i < input.Services.Count; i++)
        {
            var s = input.Services[i];
            var empty = string.IsNullOrWhiteSpace(s.Name) && s.UnitPrice is null or 0;
            if (empty) continue;
            if (string.IsNullOrWhiteSpace(s.Name)) ModelState.AddModelError($"Input.Services[{i}].Name", "Nhập tên dịch vụ.");
            if (s.UnitPrice == null) ModelState.AddModelError($"Input.Services[{i}].UnitPrice", "Nhập đơn giá.");
        }
        return area;
    }

    private static void Apply(RentalPost post, RoomInput input, double area)
    {
        post.Title = input.Title.Trim();
        post.Description = input.Description.Trim();
        post.RoomType = input.RoomType;
        post.Price = input.Price!.Value;
        post.Area = Math.Round(area, 2);
        post.Address = input.Address.Trim();
        post.Ward = input.Ward?.Trim() ?? "";
        post.District = input.District.Trim();
        post.City = input.City.Trim();
        post.MapLocation = DisplayFormat.Clean(input.MapLocation, 500);
        post.AmenitiesJson = input.Amenities.Count == 0 ? null : JsonSerializer.Serialize(input.Amenities);
        post.ElectricityUnitPrice = input.ElectricityUnitPrice ?? 0;
        post.WaterUnitPrice = input.WaterUnitPrice ?? 0;
        post.ServiceFee = input.ServiceFee ?? 0;
        post.AvailableFrom = input.AvailableFrom!.Value.Date;
    }

    private const string InternetName = "Internet";
    private static bool IsInternet(Service s) => string.Equals(s.Name, InternetName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Dịch vụ tính riêng + dòng "Phí Internet" của BM01 (chỉ lưu khi lớn hơn 0).</summary>
    private static IEnumerable<Service> CleanServices(RoomInput input)
    {
        var list = input.Services
            .Where(s => !string.IsNullOrWhiteSpace(s.Name) && s.UnitPrice.HasValue &&
                !string.Equals(s.Name.Trim(), InternetName, StringComparison.OrdinalIgnoreCase))
            .Take(10)
            .Select(s => new Service
            {
                Name = s.Name!.Trim(), UnitPrice = s.UnitPrice!.Value,
                Unit = string.IsNullOrWhiteSpace(s.Unit) ? "tháng" : s.Unit.Trim()
            }).ToList();
        if (input.InternetFee is > 0) list.Insert(0, new Service { Name = InternetName, UnitPrice = input.InternetFee.Value, Unit = "tháng" });
        return list;
    }

    /// <summary>Đọc tiện ích theo cùng định dạng phía Tenant: mảng chuỗi hoặc object {tên: true}.</summary>
    private static List<string> ReadAmenities(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 16384) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            IEnumerable<string?> names = root.ValueKind switch
            {
                JsonValueKind.Array => root.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString()).ToList(),
                JsonValueKind.Object => root.EnumerateObject().Where(x => x.Value.ValueKind == JsonValueKind.True).Select(x => (string?)x.Name).ToList(),
                _ => []
            };
            return names.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct().ToList();
        }
        catch (JsonException) { return []; }
    }

    /// <summary>Chỉ nhận link http(s) hoặc tọa độ; trả link Google Maps an toàn để hiển thị.</summary>
    private static string? MapUrl(string? location)
    {
        if (string.IsNullOrWhiteSpace(location)) return null;
        location = location.Trim();
        var parts = location.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 2 &&
            double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var lat) &&
            double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var lng) &&
            lat is >= -90 and <= 90 && lng is >= -180 and <= 180)
            return $"https://www.google.com/maps?q={lat.ToString(CultureInfo.InvariantCulture)},{lng.ToString(CultureInfo.InvariantCulture)}";
        return Uri.TryCreate(location, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.AbsoluteUri : null;
    }
}
