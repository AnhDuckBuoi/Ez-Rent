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
/// UC12 Lập hợp đồng, UC14 Xác nhận hợp đồng (phía chủ trọ), UC15 Tra cứu hợp đồng (P05, BM03).
/// Luồng trạng thái dùng enum có sẵn:
///   Draft --(Gửi)--> PendingTenant --(người thuê xác nhận*)--> Active
///   Draft/PendingTenant --(Hủy)--> Cancelled;  PendingTenant --(Thu hồi để sửa)--> Draft
///   PendingLandlord hoặc PendingTenant có TenantConfirmedAt (người thuê đã xác nhận*) --(Chủ trọ xác nhận)--> Active
///   Active --(Kết thúc)--> Expired
/// (*) Thao tác xác nhận của người thuê thuộc phía Tenant (hiện Tenant chỉ xem hợp đồng).
/// Khi Active: phòng chuyển "Đã cho thuê"; DB đã có index chặn 2 hợp đồng Active cho một phòng.
/// Xác nhận trên hệ thống không phải chữ ký số/chữ ký điện tử có giá trị pháp lý.
/// </summary>
public sealed class ContractsController(AppDbContext db, TimeProvider? clock = null) : LandlordControllerBase(db, clock)
{
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ContractListInput input)
    {
        if (!ModelState.IsValid) return InvalidQuery(Back(nameof(Index)));
        var counts = await MyContracts.AsNoTracking().GroupBy(c => c.Status)
            .Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
        var query = MyContracts.AsNoTracking();
        if (input.Status.HasValue) query = query.Where(c => c.Status == input.Status.Value);
        var total = await query.CountAsync();
        var page = PagedList<ContractRow>.ClampPage(input.Page, total, PageSize);
        var rows = await query
            .OrderBy(c => c.Status == ContractStatus.PendingLandlord ? 0 : c.Status == ContractStatus.Active ? 1
                : c.Status == ContractStatus.PendingTenant ? 2 : c.Status == ContractStatus.Draft ? 3 : 4)
            .ThenByDescending(c => c.CreatedAt).ThenByDescending(c => c.RentalContractId)
            .Skip((page - 1) * PageSize).Take(PageSize)
            .Select(c => new ContractRow
            {
                Id = c.RentalContractId, RoomId = c.RentalPostId, RoomTitle = c.RentalPost.Title, TenantName = c.Tenant.FullName,
                StartDate = c.StartDate, EndDate = c.EndDate, MonthlyRent = c.MonthlyRent, Status = c.Status, CreatedAt = c.CreatedAt
            }).ToListAsync();
        input.Page = page;
        return PageOrRegion("Index", "_ContractList", new ContractListModel
        {
            Input = input, Counts = counts,
            Items = new PagedList<ContractRow> { Items = rows, Page = page, PageSize = PageSize, Total = total }
        });
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromRoute] int id)
    {
        var c = await MyContracts.AsNoTracking().Where(x => x.RentalContractId == id).Select(x => new ContractDetailModel
        {
            Id = x.RentalContractId, Status = x.Status, RoomId = x.RentalPostId, RoomTitle = x.RentalPost.Title,
            RoomAddress = x.RentalPost.Address + ", " + x.RentalPost.District + ", " + x.RentalPost.City, RoomArea = x.RentalPost.Area,
            LandlordName = x.Landlord.FullName, LandlordPhone = x.Landlord.PhoneNumber, LandlordEmail = x.Landlord.Email,
            TenantId = x.TenantId, TenantName = x.Tenant.FullName, TenantPhone = x.Tenant.PhoneNumber, TenantEmail = x.Tenant.Email,
            StartDate = x.StartDate, EndDate = x.EndDate, MonthlyRent = x.MonthlyRent, DepositAmount = x.DepositAmount,
            AgreedElectricityPrice = x.AgreedElectricityPrice, AgreedWaterPrice = x.AgreedWaterPrice, Terms = x.Terms ?? "",
            CreatedAt = x.CreatedAt, LandlordConfirmedAt = x.LandlordConfirmedAt, TenantConfirmedAt = x.TenantConfirmedAt,
            InvoiceCount = x.Invoices.Count(),
            UnpaidInvoices = x.Invoices.Count(i => i.PaymentStatus == PaymentStatus.Unpaid || i.PaymentStatus == PaymentStatus.AwaitingConfirmation || i.PaymentStatus == PaymentStatus.Overdue)
        }).FirstOrDefaultAsync();
        return c == null ? MissingPage("hợp đồng", Back(nameof(Index))) : PageOrRegion("Details", "_ContractPanel", c);
    }

    [HttpGet]
    public async Task<IActionResult> Create([FromQuery] int? roomId, [FromQuery] string? tenantId)
    {
        var input = new ContractInput
        {
            StartDate = VietnamToday, EndDate = VietnamToday.AddMonths(12).AddDays(-1), Terms = DefaultTerms, TenantId = tenantId
        };
        if (roomId.HasValue)
        {
            var room = await RentableRooms().Where(p => p.RentalPostId == roomId.Value)
                .Select(p => new { p.RentalPostId, p.Price, p.ElectricityUnitPrice, p.WaterUnitPrice }).FirstOrDefaultAsync();
            if (room != null)
            {
                input.RentalPostId = room.RentalPostId; input.MonthlyRent = room.Price; input.DepositAmount = room.Price;
                input.AgreedElectricityPrice = room.ElectricityUnitPrice; input.AgreedWaterPrice = room.WaterUnitPrice;
            }
        }
        return View("Create", await FormAsync(input, null));
    }

    /// <summary>AJAX: đổi phòng trong form thì điền sẵn giá thuê/điện/nước theo tin đăng (chủ trọ vẫn sửa được).</summary>
    [HttpGet]
    public async Task<IActionResult> RoomDefaults([FromQuery] int roomId)
    {
        var room = await RentableRooms().Where(p => p.RentalPostId == roomId)
            .Select(p => new { p.Price, p.ElectricityUnitPrice, p.WaterUnitPrice }).FirstOrDefaultAsync();
        if (room == null) { Response.StatusCode = 404; return Json(new { ok = false, message = "Phòng không còn cho thuê được." }); }
        return Json(new { ok = true, rent = room.Price, deposit = room.Price, electricity = room.ElectricityUnitPrice, water = room.WaterUnitPrice });
    }

    [HttpPost]
    public async Task<IActionResult> Create([Bind(Prefix = "Input")] ContractInput input, [FromForm] string? intent)
    {
        var tenantId = await ValidateAsync(input, null);
        if (!ModelState.IsValid || tenantId == null) return View("Create", await FormAsync(input, null));
        var send = intent == "send";
        var contract = new RentalContract
        {
            LandlordId = CurrentUserId, TenantId = tenantId, RentalPostId = input.RentalPostId, CreatedAt = UtcNow,
            Status = send ? ContractStatus.PendingTenant : ContractStatus.Draft,
            LandlordConfirmedAt = send ? UtcNow : null
        };
        Apply(contract, input);
        Db.RentalContracts.Add(contract);
        await Db.SaveChangesAsync();
        if (send)
        {
            await NotifySentAsync(contract);
            await Db.SaveChangesAsync();
        }
        return Done(send ? "Đã lập hợp đồng và gửi cho người thuê xem, xác nhận." : "Đã lưu hợp đồng nháp.",
            Back(nameof(Details), new { id = contract.RentalContractId }));
    }

    [HttpGet]
    public async Task<IActionResult> Edit([FromRoute] int id)
    {
        var c = await MyContracts.AsNoTracking().FirstOrDefaultAsync(x => x.RentalContractId == id);
        if (c == null) return MissingPage("hợp đồng", Back(nameof(Index)));
        if (c.Status != ContractStatus.Draft)
            return Fail(StatusCodes.Status409Conflict, "Chỉ sửa được hợp đồng nháp. Hãy thu hồi hợp đồng đang chờ người thuê trước.", Back(nameof(Details), new { id }));
        var input = new ContractInput
        {
            RentalPostId = c.RentalPostId, TenantId = c.TenantId, StartDate = c.StartDate.Date, EndDate = c.EndDate.Date,
            MonthlyRent = c.MonthlyRent, DepositAmount = c.DepositAmount, AgreedElectricityPrice = c.AgreedElectricityPrice,
            AgreedWaterPrice = c.AgreedWaterPrice, Terms = c.Terms ?? ""
        };
        return View("Edit", await FormAsync(input, id));
    }

    [HttpPost]
    public async Task<IActionResult> Edit([FromRoute] int id, [Bind(Prefix = "Input")] ContractInput input)
    {
        var c = await MyContracts.FirstOrDefaultAsync(x => x.RentalContractId == id);
        if (c == null) return MissingPage("hợp đồng", Back(nameof(Index)));
        if (c.Status != ContractStatus.Draft)
            return Fail(StatusCodes.Status409Conflict, "Hợp đồng không còn là bản nháp nên không sửa được.", Back(nameof(Details), new { id }));
        // Người thuê của hợp đồng đã lập không đổi được: muốn đổi người thì hủy và lập hợp đồng mới.
        input.TenantId = c.TenantId; input.TenantEmail = null;
        var tenantId = await ValidateAsync(input, c);
        if (!ModelState.IsValid || tenantId == null) return View("Edit", await FormAsync(input, id));
        c.RentalPostId = input.RentalPostId;
        Apply(c, input);
        await Db.SaveChangesAsync();
        return Done("Đã lưu thay đổi hợp đồng nháp.", Back(nameof(Details), new { id }));
    }

    [HttpPost]
    public async Task<IActionResult> Send([FromRoute] int id)
    {
        var back = Back(nameof(Details), new { id });
        var c = await MyContracts.AsNoTracking().Include(x => x.RentalPost).FirstOrDefaultAsync(x => x.RentalContractId == id);
        if (c == null) return MissingPage("hợp đồng", Back(nameof(Index)));
        if (c.Status != ContractStatus.Draft) return Fail(StatusCodes.Status409Conflict, "Chỉ gửi được hợp đồng nháp.", back);
        if (c.RentalPost.RoomStatus == RoomStatus.Rented)
            return Fail(StatusCodes.Status409Conflict, "Phòng đang có hợp đồng hiệu lực khác.", back);
        var changed = await Db.RentalContracts.Where(x => x.RentalContractId == id && x.Status == ContractStatus.Draft)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ContractStatus.PendingTenant)
                .SetProperty(x => x.LandlordConfirmedAt, UtcNow).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Trạng thái hợp đồng vừa thay đổi, hãy tải lại.", back);
        await NotifySentAsync(c);
        await Db.SaveChangesAsync();
        return Done("Đã gửi hợp đồng cho người thuê xác nhận.", back);
    }

    [HttpPost]
    public async Task<IActionResult> Recall([FromRoute] int id)
    {
        var back = Back(nameof(Details), new { id });
        var c = await MyContracts.AsNoTracking().FirstOrDefaultAsync(x => x.RentalContractId == id);
        if (c == null) return MissingPage("hợp đồng", Back(nameof(Index)));
        var changed = await Db.RentalContracts
            .Where(x => x.RentalContractId == id && x.Status == ContractStatus.PendingTenant && x.TenantConfirmedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ContractStatus.Draft)
                .SetProperty(x => x.LandlordConfirmedAt, (DateTime?)null).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Chỉ thu hồi được hợp đồng đang chờ và người thuê chưa xác nhận.", back);
        LandlordNotifier.Add(Db, c.TenantId, "Chủ nhà thu hồi hợp đồng để điều chỉnh",
            "Hợp đồng đang được chỉnh sửa theo trao đổi của hai bên. Bạn sẽ nhận thông báo khi chủ nhà gửi lại.",
            LandlordNotifier.ContractLink(id), "contract", UtcNow);
        await Db.SaveChangesAsync();
        return Done("Đã thu hồi hợp đồng về bản nháp để chỉnh sửa.", back);
    }

    [HttpPost]
    public async Task<IActionResult> Confirm([FromRoute] int id)
    {
        var back = Back(nameof(Details), new { id });
        var c = await MyContracts.Include(x => x.RentalPost).FirstOrDefaultAsync(x => x.RentalContractId == id);
        if (c == null) return MissingPage("hợp đồng", Back(nameof(Index)));
        // Phía Tenant có thể ghi TenantConfirmedAt rồi chuyển PendingLandlord, hoặc giữ PendingTenant: chấp nhận cả hai.
        if (c.TenantConfirmedAt == null || c.Status is not (ContractStatus.PendingLandlord or ContractStatus.PendingTenant))
            return Fail(StatusCodes.Status409Conflict, "Người thuê cần xác nhận trước khi bạn xác nhận hiệu lực.", back);
        if (c.RentalPost.OwnerId != CurrentUserId) return MissingPage("hợp đồng", Back(nameof(Index)));
        await using var tx = await Db.Database.BeginTransactionAsync();
        if (await Db.RentalContracts.AnyAsync(x => x.RentalPostId == c.RentalPostId && x.Status == ContractStatus.Active))
            return Fail(StatusCodes.Status409Conflict, "Phòng đã có hợp đồng hiệu lực khác.", back);
        c.Status = ContractStatus.Active;
        c.LandlordConfirmedAt = UtcNow;
        c.RentalPost.RoomStatus = RoomStatus.Rented;
        LandlordNotifier.Add(Db, c.TenantId, "Hợp đồng thuê phòng đã có hiệu lực",
            $"{c.RentalPost.Title}: từ {LandlordTime.Date(c.StartDate)} đến {LandlordTime.Date(c.EndDate)}.",
            LandlordNotifier.ContractLink(id), "contract", UtcNow);
        try
        {
            await Db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            return Fail(StatusCodes.Status409Conflict, "Phòng đã có hợp đồng hiệu lực khác.", back);
        }
        return Done("Hợp đồng đã có hiệu lực. Phòng chuyển sang “Đã cho thuê”.", back);
    }

    [HttpPost]
    public async Task<IActionResult> Cancel([FromRoute] int id)
    {
        var back = Back(nameof(Details), new { id });
        var c = await MyContracts.AsNoTracking().FirstOrDefaultAsync(x => x.RentalContractId == id);
        if (c == null) return MissingPage("hợp đồng", Back(nameof(Index)));
        var changed = await Db.RentalContracts.Where(x => x.RentalContractId == id &&
                (x.Status == ContractStatus.Draft || x.Status == ContractStatus.PendingTenant || x.Status == ContractStatus.PendingLandlord))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, ContractStatus.Cancelled).SetProperty(x => x.UpdatedAt, UtcNow));
        if (changed == 0) return Fail(StatusCodes.Status409Conflict, "Chỉ hủy được hợp đồng chưa có hiệu lực. Hợp đồng đang hiệu lực hãy dùng “Kết thúc hợp đồng”.", back);
        if (c.Status != ContractStatus.Draft)
        {
            LandlordNotifier.Add(Db, c.TenantId, "Hợp đồng đã bị hủy", "Chủ nhà đã hủy hợp đồng chưa có hiệu lực. Liên hệ chủ nhà nếu cần trao đổi thêm.",
                LandlordNotifier.ContractLink(id), "contract", UtcNow);
            await Db.SaveChangesAsync();
        }
        return Done("Đã hủy hợp đồng. Lịch sử vẫn được lưu để tra cứu.", back);
    }

    [HttpPost]
    public async Task<IActionResult> End([FromRoute] int id)
    {
        var back = Back(nameof(Details), new { id });
        var c = await MyContracts.Include(x => x.RentalPost).FirstOrDefaultAsync(x => x.RentalContractId == id);
        if (c == null) return MissingPage("hợp đồng", Back(nameof(Index)));
        if (c.Status != ContractStatus.Active) return Fail(StatusCodes.Status409Conflict, "Chỉ kết thúc được hợp đồng đang hiệu lực.", back);
        await using var tx = await Db.Database.BeginTransactionAsync();
        c.Status = ContractStatus.Expired;
        // Phòng trống lại nhưng vẫn giữ trạng thái duyệt; chủ trọ có thể ẩn tin nếu chưa muốn cho thuê tiếp.
        if (c.RentalPost.OwnerId == CurrentUserId && c.RentalPost.RoomStatus == RoomStatus.Rented) c.RentalPost.RoomStatus = RoomStatus.Available;
        LandlordNotifier.Add(Db, c.TenantId, "Hợp đồng thuê phòng đã kết thúc",
            $"{c.RentalPost.Title}: hợp đồng kết thúc ngày {LandlordTime.Date(VietnamToday)}.", LandlordNotifier.ContractLink(id), "contract", UtcNow);
        await Db.SaveChangesAsync();
        await tx.CommitAsync();
        var unpaid = await Db.Invoices.CountAsync(i => i.RentalContractId == id &&
            (i.PaymentStatus == PaymentStatus.Unpaid || i.PaymentStatus == PaymentStatus.AwaitingConfirmation || i.PaymentStatus == PaymentStatus.Overdue));
        return Done("Đã kết thúc hợp đồng, phòng trở lại trạng thái còn trống." +
            (unpaid > 0 ? $" Lưu ý: còn {unpaid} hóa đơn chưa thanh toán." : ""), back);
    }

    // ===================== Hỗ trợ =====================

    private IQueryable<RentalPost> RentableRooms() => MyPosts.Where(p =>
        p.ApprovalStatus == ApprovalStatus.Approved && p.RoomStatus != RoomStatus.Rented &&
        !p.RentalContracts.Any(c => c.Status == ContractStatus.Active));

    private async Task<ContractFormModel> FormAsync(ContractInput input, int? id)
    {
        var me = CurrentUserId;
        var rooms = await RentableRooms().OrderBy(p => p.Title)
            .Select(p => new OptionItem(p.RentalPostId.ToString(), p.Title, p.Address + ", " + p.District)).ToListAsync();
        // Gợi ý người thuê đã xem phòng / nhắn tin với mình; vẫn có thể nhập email tài khoản.
        var fromViewings = await MyViewings.Where(a => a.Status == ViewingAppointmentStatus.Confirmed || a.Status == ViewingAppointmentStatus.Completed)
            .Select(a => new { a.TenantId, a.Tenant.FullName, a.Tenant.Email, Room = a.RentalPost.Title }).Distinct().Take(200).ToListAsync();
        var fromChats = await MyConversations.Select(c => new
            {
                TenantId = c.InitiatorId == me ? c.RecipientId : c.InitiatorId,
                FullName = c.InitiatorId == me ? c.Recipient.FullName : c.Initiator.FullName,
                Email = c.InitiatorId == me ? c.Recipient.Email : c.Initiator.Email
            }).Distinct().Take(200).ToListAsync();
        var tenants = fromViewings.Select(t => new OptionItem(t.TenantId, t.FullName, $"{MaskEmail(t.Email)} · đã xem {t.Room}"))
            .Concat(fromChats.Select(t => new OptionItem(t.TenantId, t.FullName, $"{MaskEmail(t.Email)} · có tin nhắn")))
            .GroupBy(t => t.Value).Select(g => g.First()).OrderBy(t => t.Text).ToList();
        string? tenantName = null;
        if (id.HasValue && !string.IsNullOrEmpty(input.TenantId))
            tenantName = await Db.Users.Where(u => u.Id == input.TenantId).Select(u => u.FullName).FirstOrDefaultAsync();
        // Khi sửa: luôn giữ phòng hiện tại trong danh sách dù phòng không còn "trống".
        if (input.RentalPostId > 0 && rooms.All(r => r.Value != input.RentalPostId.ToString()))
        {
            var current = await MyPosts.Where(p => p.RentalPostId == input.RentalPostId)
                .Select(p => new OptionItem(p.RentalPostId.ToString(), p.Title, p.Address)).FirstOrDefaultAsync();
            if (current != null) rooms.Insert(0, current);
        }
        return new ContractFormModel { Id = id, Input = input, Rooms = rooms, Tenants = tenants, TenantName = tenantName };
    }

    private async Task<string?> ValidateAsync(ContractInput input, RentalContract? existing)
    {
        var room = await MyPosts.AsNoTracking().Where(p => p.RentalPostId == input.RentalPostId)
            .Select(p => new { p.ApprovalStatus, p.RoomStatus, HasActive = p.RentalContracts.Any(c => c.Status == ContractStatus.Active) })
            .FirstOrDefaultAsync();
        if (room == null) ModelState.AddModelError("Input.RentalPostId", "Chọn phòng của bạn.");
        else if (room.ApprovalStatus != ApprovalStatus.Approved) ModelState.AddModelError("Input.RentalPostId", "Phòng phải được duyệt trước khi lập hợp đồng.");
        else if (room.HasActive || room.RoomStatus == RoomStatus.Rented) ModelState.AddModelError("Input.RentalPostId", "Phòng đang có hợp đồng hiệu lực.");

        if (input.StartDate.HasValue && input.EndDate.HasValue)
        {
            var start = input.StartDate.Value.Date; var end = input.EndDate.Value.Date;
            if (end <= start) ModelState.AddModelError("Input.EndDate", "Ngày kết thúc phải sau ngày bắt đầu.");
            else if (end > start.AddYears(5)) ModelState.AddModelError("Input.EndDate", "Thời hạn tối đa 5 năm.");
            if (existing == null && start < VietnamToday.AddDays(-31))
                ModelState.AddModelError("Input.StartDate", "Ngày bắt đầu không sớm hơn 1 tháng trước.");
        }

        string? tenantId = null;
        if (!string.IsNullOrWhiteSpace(input.TenantId)) tenantId = input.TenantId.Trim();
        else if (!string.IsNullOrWhiteSpace(input.TenantEmail))
        {
            var normalized = input.TenantEmail.Trim().ToUpperInvariant();
            tenantId = await Db.Users.Where(u => u.NormalizedEmail == normalized).Select(u => u.Id).FirstOrDefaultAsync();
            if (tenantId == null) { ModelState.AddModelError("Input.TenantEmail", "Không có tài khoản nào dùng email này."); return null; }
        }
        else { ModelState.AddModelError("Input.TenantId", "Chọn người thuê hoặc nhập email tài khoản của họ."); return null; }

        var tenant = await Db.Users.AsNoTracking().Where(u => u.Id == tenantId)
            .Select(u => new
            {
                u.Status,
                IsTenant = Db.UserRoles.Any(ur => ur.UserId == u.Id && Db.Roles.Any(r => r.Id == ur.RoleId && r.NormalizedName == "TENANT"))
            }).FirstOrDefaultAsync();
        if (tenant == null || !tenant.IsTenant || tenantId == CurrentUserId)
        { ModelState.AddModelError("Input.TenantId", "Tài khoản được chọn không phải người thuê."); return null; }
        if (tenant.Status != UserStatus.Active)
        { ModelState.AddModelError("Input.TenantId", "Tài khoản người thuê đang bị khóa."); return null; }
        return tenantId;
    }

    private static void Apply(RentalContract c, ContractInput input)
    {
        c.StartDate = input.StartDate!.Value.Date;
        c.EndDate = input.EndDate!.Value.Date;
        c.MonthlyRent = input.MonthlyRent!.Value;
        c.DepositAmount = input.DepositAmount!.Value;
        c.AgreedElectricityPrice = input.AgreedElectricityPrice!.Value;
        c.AgreedWaterPrice = input.AgreedWaterPrice!.Value;
        c.Terms = input.Terms.Trim();
    }

    private async Task NotifySentAsync(RentalContract c)
    {
        var title = c.RentalPost?.Title ?? await Db.RentalPosts.Where(p => p.RentalPostId == c.RentalPostId).Select(p => p.Title).FirstAsync();
        LandlordNotifier.Add(Db, c.TenantId, "Bạn có hợp đồng thuê phòng cần xem",
            $"{title}: {LandlordFormat.Money(c.MonthlyRent)}/tháng, từ {LandlordTime.Date(c.StartDate)} đến {LandlordTime.Date(c.EndDate)}. Hãy xem kỹ điều khoản trước khi xác nhận.",
            LandlordNotifier.ContractLink(c.RentalContractId), "contract", UtcNow);
    }

    private static string MaskEmail(string? email)
    {
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@')) return "chưa có email";
        var at = email.IndexOf('@');
        return (at <= 2 ? email[..1] : email[..2]) + "•••" + email[at..];
    }

    private const string DefaultTerms =
@"1. Trách nhiệm của bên cho thuê
- Bàn giao phòng và trang thiết bị đúng hiện trạng đã thỏa thuận vào ngày bắt đầu hợp đồng.
- Bảo đảm quyền sử dụng phòng ổn định cho bên thuê trong thời hạn hợp đồng.
- Sửa chữa hư hỏng không do lỗi bên thuê trong thời gian hợp lý kể từ khi nhận yêu cầu trên EZ-Rent.
- Ghi chỉ số điện nước hằng tháng kèm ảnh công tơ và lập hóa đơn minh bạch trên hệ thống.

2. Trách nhiệm của bên thuê
- Thanh toán tiền phòng, điện, nước và dịch vụ đúng hạn ghi trên hóa đơn.
- Sử dụng phòng đúng mục đích để ở; giữ gìn tài sản, bồi thường hư hỏng do lỗi của mình.
- Chấp hành quy định về an ninh, phòng cháy chữa cháy, vệ sinh chung và giờ giấc của khu trọ.
- Không tự ý cho thuê lại hoặc chuyển nhượng phòng khi chưa có sự đồng ý của bên cho thuê.

3. Tiền đặt cọc
- Tiền cọc được hoàn trả khi kết thúc hợp đồng, sau khi trừ các khoản còn nợ hoặc chi phí sửa chữa hư hỏng do bên thuê gây ra.
- Bên thuê chấm dứt hợp đồng trước hạn phải báo trước ít nhất 30 ngày; nếu không báo trước có thể không được hoàn cọc.

4. Thỏa thuận chung
- Hai bên cam kết thực hiện đúng các điều khoản trên. Mọi thay đổi được hai bên trao đổi và cập nhật trên hệ thống trước khi xác nhận.
- Việc xác nhận trên EZ-Rent ghi nhận sự đồng ý của hai bên, không thay thế chữ ký số hoặc chữ ký điện tử theo quy định pháp luật.";
}
