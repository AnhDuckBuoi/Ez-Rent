using System.Globalization;
using System.Text.Json;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using EZ_Rent.Services;
using EZ_Rent.Services.Billing;
using EZ_Rent.Services.Common;
using EZ_Rent.Services.Contracts;
using EZ_Rent.Services.Meters;
using EZ_Rent.Services.Notifications;
using EZ_Rent.Services.Payments;
using EZ_Rent.Services.Viewings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Data.Seeder;

/// <summary>
/// Dữ liệu mẫu cho khu CHỦ TRỌ để demo và kiểm thử: một chủ trọ (chị Lan) với 7 tin phòng ở nhiều trạng thái,
/// khung giờ nhận khách, lịch xem, tin nhắn, hợp đồng kèm tài liệu đã đối chiếu, chỉ số điện nước có ảnh công tơ,
/// hóa đơn VietQR kèm bộ đối chiếu, yêu cầu sửa chữa có lịch sử xử lý và yêu cầu hỗ trợ có phản hồi của Admin.
/// - Chạy sau DbSeeder của nhóm (cần vai trò và tài khoản Admin), qua LandlordDemoSeedRunner khi khởi động.
/// - Chỉ chạy ở môi trường Development/Demo (hoặc khi cấu hình LandlordDemo:Seed = true); có tài khoản chủ trọ mẫu rồi thì bỏ qua.
/// - Ảnh công tơ và tài liệu hợp đồng đi qua đúng RentalPrivateFileStore/RentalDocumentService như khi chủ trọ thao tác thật.
/// Tài khoản: chutro.demo@ezrent.vn / Landlord@123; người thuê nguoithue1.demo@ezrent.vn … nguoithue8.demo@ezrent.vn / Tenant@123.
/// </summary>
public static class LandlordDemoSeed
{
    public const string LandlordEmail = "chutro.demo@ezrent.vn";
    public const string LandlordPassword = "Landlord@123";
    public const string TenantPassword = "Tenant@123";

    private static readonly (string Name, string Phone)[] TenantInfo =
    [
        ("Trần Minh Anh", "0912000101"), ("Lê Hoàng Nam", "0912000102"), ("Phạm Thu Hà", "0912000103"),
        ("Đỗ Quang Huy", "0912000104"), ("Vũ Ngọc Mai", "0912000105"), ("Bùi Thanh Tùng", "0912000106"),
        ("Hoàng Khánh Linh", "0912000107"), ("Ngô Đức Long", "0912000108")
    ];

    private const string Terms = """
        Điều 1. Bên A cho Bên B thuê phòng trọ nêu trên để ở; Bên B không cho người khác thuê lại khi chưa có đồng ý của Bên A.
        Điều 2. Tiền phòng thanh toán hằng tháng theo hóa đơn trên EZ-Rent, chậm nhất ngày 10 của tháng kế tiếp.
        Điều 3. Tiền điện, nước tính theo chỉ số công tơ ghi cuối tháng, có ảnh công tơ kèm theo hóa đơn.
        Điều 4. Tiền đặt cọc được hoàn trả khi kết thúc hợp đồng, sau khi trừ chi phí hư hỏng (nếu có).
        Điều 5. Bên nào muốn chấm dứt hợp đồng trước hạn phải báo trước ít nhất 30 ngày.
        Điều 6. Bên B giữ gìn tài sản, báo sự cố qua mục Yêu cầu sửa chữa; Bên A xử lý trong thời gian sớm nhất.
        """;

    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var documents = services.GetRequiredService<RentalDocumentService>();
        var files = services.GetRequiredService<RentalPrivateFileStore>();

        if (await users.FindByEmailAsync(LandlordEmail) != null) return; // đã có dữ liệu mẫu
        foreach (var role in new[] { "Landlord", "Tenant", "Admin" })
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
        var admin = (await users.GetUsersInRoleAsync("Admin")).OrderBy(u => u.Id).FirstOrDefault();
        if (admin == null) { logger.LogWarning("Chưa có tài khoản Admin; bỏ qua dữ liệu mẫu chủ trọ."); return; }

        var nowUtc = DateTime.UtcNow;
        var vnNow = VietnamTime.ToVietnam(nowUtc);
        var today = vnNow.Date;
        var thisMonth = new DateTime(today.Year, today.Month, 1);
        static DateTime Utc(DateTime vnLocal) => DateTime.SpecifyKind(vnLocal.AddHours(-7), DateTimeKind.Utc);

        // ---------- Tài khoản ----------
        async Task<ApplicationUser> CreateUser(string email, string name, string phone, string role, string password, DateTime createdUtc)
        {
            var existing = await users.FindByEmailAsync(email);
            if (existing != null) return existing;
            var user = new ApplicationUser
            {
                UserName = email, Email = email, FullName = name, PhoneNumber = phone, EmailConfirmed = true,
                Status = UserStatus.Active, CreatedAt = createdUtc
            };
            var result = await users.CreateAsync(user, password);
            if (!result.Succeeded) throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
            await users.AddToRoleAsync(user, role);
            return user;
        }
        var lan = await CreateUser(LandlordEmail, "Nguyễn Thị Lan", "0904123456", "Landlord", LandlordPassword, Utc(thisMonth.AddMonths(-6)));
        var t = new List<ApplicationUser>();
        for (var i = 0; i < TenantInfo.Length; i++)
            t.Add(await CreateUser($"nguoithue{i + 1}.demo@ezrent.vn", TenantInfo[i].Name, TenantInfo[i].Phone, "Tenant", TenantPassword,
                Utc(thisMonth.AddMonths(-6).AddDays(i * 3))));

        // ---------- Thiết lập của chủ trọ (claim, không đổi schema) ----------
        var bank = new BankAccount("970436", "0011001234567", "NGUYEN THI LAN");
        await BankAccountStore.SaveAsync(db, lan.Id, bank, ct);
        await LandlordLegalProfileStore.SaveAsync(db, lan.Id, new LandlordLegalProfile(new DateOnly(1985, 4, 12),
            IdentityDocumentKind.CitizenIdentity, "001185012345", new DateOnly(2021, 5, 20),
            "Cục Cảnh sát QLHC về trật tự xã hội", "Số 12 ngõ 96 Định Công, Hoàng Mai, Hà Nội"), ct);
        var hours = new ViewingHours();
        for (var d = 0; d < 7; d++) hours.Weekly[d] = ["08:00", "09:30", "17:30", "19:00"];
        hours.Dates[DateOnly.FromDateTime(today.AddDays(7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)] = [];
        await ViewingHoursStore.SaveAsync(db, lan.Id, hours, ct);

        // ---------- Tin phòng ----------
        RentalPost Room(string code, string title, RoomType type, decimal price, double area, string description, string[] amenities,
            ApprovalStatus approval, RoomStatus status, DateTime createdUtc, string? note = null)
        {
            var post = new RentalPost
            {
                Title = title, Description = description, Price = price, Area = area, RoomType = type,
                Address = $"Phòng {code}, ngõ 96 Định Công", Ward = "Định Công", District = "Hoàng Mai", City = "Hà Nội",
                MapLocation = "20.9822, 105.8396", AmenitiesJson = JsonSerializer.Serialize(amenities),
                ElectricityUnitPrice = 3500, WaterUnitPrice = 25000, ServiceFee = 100000,
                ApprovalStatus = approval, RoomStatus = status, ApprovalNote = note, OwnerId = lan.Id,
                AvailableFrom = today, CreatedAt = createdUtc, UpdatedAt = createdUtc
            };
            if (approval == ApprovalStatus.Approved)
            {
                post.ApprovedById = admin.Id; post.ApprovedAt = createdUtc.AddDays(1); post.PostedAt = createdUtc.AddDays(1);
            }
            for (var i = 0; i < 3; i++)
                post.RoomImages.Add(new RoomImage
                {
                    MediaUrl = DemoRoomImages.For(type, int.Parse(code), i, title.Contains("gác")), MediaType = MediaType.Image,
                    IsPrimary = i == 0, DisplayOrder = i, CreatedAt = createdUtc
                });
            post.Services.Add(new Service { Name = "Internet", UnitPrice = 100000, Unit = "tháng" });
            post.Services.Add(new Service { Name = "Gửi xe máy", UnitPrice = 100000, Unit = "xe" });
            db.RentalPosts.Add(post);
            return post;
        }
        var old = Utc(thisMonth.AddMonths(-5).AddDays(2).AddHours(9));
        var r101 = Room("101", "Phòng 101 · Studio 25 m² có gác xép, gần ĐH Mở Hà Nội", RoomType.Room, 3_200_000, 25,
            "Phòng studio 25 m² có gác xép, cửa sổ thoáng, WC khép kín, nóng lạnh. Cách ĐH Mở Hà Nội 5 phút đi xe máy, gần chợ và bến xe buýt.",
            ["Wifi", "Điều hòa", "Nóng lạnh", "Giường", "Tủ quần áo", "WC khép kín", "Chỗ để xe", "Giờ giấc tự do"], ApprovalStatus.Approved, RoomStatus.Rented, old);
        var r102 = Room("102", "Phòng 102 · 22 m² ban công thoáng, đủ nội thất", RoomType.Room, 3_000_000, 22,
            "Phòng 22 m² có ban công riêng, đầy đủ giường tủ, điều hòa, máy giặt chung tầng 1. Khu dân cư yên tĩnh, có camera an ninh.",
            ["Wifi", "Điều hòa", "Nóng lạnh", "Máy giặt", "Ban công", "Camera an ninh", "Chỗ để xe"], ApprovalStatus.Approved, RoomStatus.Rented, old.AddDays(1));
        var r201 = Room("201", "Phòng 201 · Căn hộ mini 35 m² một phòng ngủ", RoomType.Apartment, 4_500_000, 35,
            "Căn hộ mini 35 m² gồm phòng ngủ riêng, phòng khách và bếp riêng, có thang máy. Phù hợp vợ chồng trẻ hoặc người đi làm.",
            ["Wifi", "Điều hòa", "Nóng lạnh", "Tủ lạnh", "Bếp riêng", "WC khép kín", "Thang máy", "Không chung chủ"], ApprovalStatus.Approved, RoomStatus.Rented, old.AddDays(2));
        var r202 = Room("202", "Phòng 202 · 20 m² cửa sổ lớn, giá sinh viên", RoomType.Room, 2_600_000, 20,
            "Phòng 20 m² cửa sổ lớn đón nắng sáng, có điều hòa và nóng lạnh, WC khép kín. Giá phù hợp sinh viên, giờ giấc tự do.",
            ["Wifi", "Điều hòa", "Nóng lạnh", "WC khép kín", "Chỗ để xe", "Giờ giấc tự do"], ApprovalStatus.Approved, RoomStatus.Available, old.AddDays(3));
        var r301 = Room("301", "Phòng 301 · 28 m² mới sửa, khép kín", RoomType.Room, 3_400_000, 28,
            "Phòng 28 m² vừa sửa lại sàn và sơn mới, WC khép kín, có điều hòa, tủ lạnh nhỏ. Đang chờ quản trị viên duyệt tin.",
            ["Wifi", "Điều hòa", "Nóng lạnh", "Tủ lạnh", "WC khép kín", "Chỗ để xe"], ApprovalStatus.Pending, RoomStatus.Available, nowUtc.AddDays(-2));
        var r302 = Room("302", "Phòng 302 · 18 m² giá rẻ cho người đi làm", RoomType.Room, 2_200_000, 18,
            "Phòng 18 m² giá rẻ, phù hợp người đi làm cần chỗ ở gọn, gần đường lớn. Có chỗ để xe và nóng lạnh.",
            ["Wifi", "Nóng lạnh", "Chỗ để xe"], ApprovalStatus.NeedRevision, RoomStatus.Available, nowUtc.AddDays(-6),
            "Ảnh chưa thể hiện nhà vệ sinh và chỗ để xe. Vui lòng bổ sung ảnh thật rồi gửi duyệt lại.");
        Room("303", "Phòng 303 · 30 m² tầng thượng có sân phơi", RoomType.Room, 3_600_000, 30,
            "Phòng 30 m² trên tầng thượng, có sân phơi riêng và cửa sổ hai hướng. Đang hoàn thiện ảnh trước khi gửi duyệt.",
            ["Wifi", "Điều hòa", "Nóng lạnh", "Ban công", "WC khép kín"], ApprovalStatus.Draft, RoomStatus.Available, nowUtc.AddDays(-1));
        await db.SaveChangesAsync(ct);
        foreach (var tenant in new[] { t[4], t[5], t[6] })
            db.Favorites.Add(new Favorite { UserId = tenant.Id, RentalPostId = r202.RentalPostId, CreatedAt = nowUtc.AddDays(-3) });

        // ---------- Hợp đồng (3 đang hiệu lực, 1 chờ kích hoạt, 1 đã kết thúc) ----------
        (RentalPost Room, ApplicationUser Tenant, DateTime Start)[] rented =
            [(r101, t[0], thisMonth.AddMonths(-4)), (r102, t[1], thisMonth.AddMonths(-3)), (r201, t[2], thisMonth.AddMonths(-2).AddDays(4))];
        var contracts = new List<RentalContract>();
        foreach (var (room, tenant, start) in rented)
        {
            var c = new RentalContract
            {
                RentalPostId = room.RentalPostId, TenantId = tenant.Id, LandlordId = lan.Id, StartDate = start,
                EndDate = start.AddMonths(12).AddDays(-1), MonthlyRent = room.Price, DepositAmount = room.Price,
                AgreedElectricityPrice = room.ElectricityUnitPrice, AgreedWaterPrice = room.WaterUnitPrice,
                Terms = ContractTermsHeader.Compose("both", "Hà Nội", Terms), Status = ContractStatus.Active,
                LandlordConfirmedAt = Utc(start.AddDays(-2).AddHours(10)), TenantConfirmedAt = Utc(start.AddDays(-2).AddHours(10)),
                CreatedAt = Utc(start.AddDays(-4).AddHours(9))
            };
            db.RentalContracts.Add(c); contracts.Add(c);
            db.ViewingAppointments.Add(new ViewingAppointment
            {
                RentalPostId = room.RentalPostId, TenantId = tenant.Id, ViewingDate = start.AddDays(-6).AddHours(17).AddMinutes(30),
                Status = ViewingAppointmentStatus.Completed, Note = "Em muốn xem phòng sau giờ học ạ.",
                LandlordNote = "Đã xem phòng, khách ưng ý.", CreatedAt = Utc(start.AddDays(-8).AddHours(20))
            });
        }
        var pending = new RentalContract
        {
            RentalPostId = r202.RentalPostId, TenantId = t[3].Id, LandlordId = lan.Id, StartDate = today.AddDays(5),
            EndDate = today.AddDays(5).AddMonths(6).AddDays(-1), MonthlyRent = r202.Price, DepositAmount = r202.Price,
            AgreedElectricityPrice = 3500, AgreedWaterPrice = 25000, Terms = ContractTermsHeader.Compose("transfer", "Hà Nội", Terms),
            Status = ContractStatus.PendingTenant, LandlordConfirmedAt = nowUtc.AddDays(-1), CreatedAt = nowUtc.AddDays(-1).AddHours(-1)
        };
        db.RentalContracts.Add(pending);
        var expiredStart = thisMonth.AddMonths(-16);
        db.RentalContracts.Add(new RentalContract
        {
            RentalPostId = r102.RentalPostId, TenantId = t[7].Id, LandlordId = lan.Id, StartDate = expiredStart,
            EndDate = thisMonth.AddMonths(-4).AddDays(-1), MonthlyRent = 2_800_000, DepositAmount = 2_800_000,
            AgreedElectricityPrice = 3500, AgreedWaterPrice = 25000, Terms = ContractTermsHeader.Compose("cash", "Hà Nội", Terms),
            Status = ContractStatus.Expired, LandlordConfirmedAt = Utc(expiredStart.AddDays(-1)), TenantConfirmedAt = Utc(expiredStart.AddDays(-1)),
            CreatedAt = Utc(expiredStart.AddDays(-3))
        });

        // ---------- Lịch xem phòng 202 (đủ các trạng thái) ----------
        void Viewing(ApplicationUser tenant, DateTime vnTime, ViewingAppointmentStatus status, DateTime createdUtc, string? note, string? reply) =>
            db.ViewingAppointments.Add(new ViewingAppointment
            {
                RentalPostId = r202.RentalPostId, TenantId = tenant.Id, ViewingDate = vnTime, Status = status,
                Note = note, LandlordNote = reply, CreatedAt = createdUtc
            });
        Viewing(t[3], today.AddDays(-6).AddHours(9).AddMinutes(30), ViewingAppointmentStatus.Completed, Utc(today.AddDays(-8).AddHours(21)),
            "Em muốn xem phòng để chuyển đến đầu tháng sau.", "Khách đã xem, thống nhất lập hợp đồng.");
        Viewing(t[5], today.AddDays(-3).AddHours(19), ViewingAppointmentStatus.Rejected, Utc(today.AddDays(-4).AddHours(8)),
            "Cho em xem phòng buổi tối ạ.", "Giờ này mình bận, bạn chọn khung giờ khác giúp mình nhé.");
        Viewing(t[4], today.AddDays(-2).AddHours(9).AddMinutes(30), ViewingAppointmentStatus.Cancelled, Utc(today.AddDays(-3).AddHours(10)),
            "Em bận đột xuất nên xin hủy lịch ạ.", null);
        Viewing(t[6], today.AddDays(1).AddHours(17).AddMinutes(30), ViewingAppointmentStatus.Confirmed, nowUtc.AddHours(-20),
            "Em đi làm về khoảng 17 giờ, xem được không chị?", "Đã xác nhận, bạn gọi mình khi tới cổng.");
        Viewing(t[4], today.AddDays(1).AddHours(9).AddMinutes(30), ViewingAppointmentStatus.Pending, nowUtc.AddMinutes(-40),
            "Em là sinh viên năm 3, muốn xem phòng buổi sáng ạ.", null);
        Viewing(t[5], today.AddDays(2).AddHours(19), ViewingAppointmentStatus.Pending, nowUtc.AddHours(-5),
            "Cho em xem phòng sau giờ làm ạ.", null);
        await db.SaveChangesAsync(ct);

        // ---------- Tài liệu hợp đồng (PDF riêng tư, người thuê đã đối chiếu) ----------
        IFormFile Form(byte[] bytes, string fileName) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", fileName)
        { Headers = new HeaderDictionary(), ContentType = fileName.EndsWith(".pdf") ? "application/pdf" : "image/png" };
        async Task Document(RentalContract c, string roomCode, DateTime acknowledgedUtc)
        {
            var tenant = t.First(x => x.Id == c.TenantId);
            var pdf = LandlordDemoImages.ContractPdf(
            [
                "HOP DONG THUE PHONG TRO", $"So {c.RentalContractId}/EZ-RENT - Phong {roomCode}, ngo 96 Dinh Cong, Hoang Mai, Ha Noi",
                $"Ben A (cho thue): {DisplayFormat.Ascii(lan.FullName)} - {lan.PhoneNumber}",
                $"Ben B (thue): {DisplayFormat.Ascii(tenant.FullName)} - {tenant.PhoneNumber}",
                $"Thoi han: {c.StartDate:dd/MM/yyyy} - {c.EndDate:dd/MM/yyyy}",
                $"Tien phong: {c.MonthlyRent:N0} d/thang - Dat coc: {c.DepositAmount:N0} d",
                "Dien 3.500 d/kWh, nuoc 25.000 d/m3 theo chi so cong to cuoi thang.",
                "Hai ben da doc, thong nhat va ky ban giay; ban nay luu tren EZ-Rent de doi chieu."
            ]);
            var doc = await documents.AppendContractAsync(c.RentalContractId, lan.Id, 0, Form(pdf, $"hop-dong-{roomCode}.pdf"), ct);
            await db.ContractDocuments.Where(d => d.ContractDocumentId == doc.ContractDocumentId)
                .ExecuteUpdateAsync(s => s.SetProperty(d => d.TenantAcknowledgedAt, acknowledgedUtc), ct);
        }
        await Document(contracts[0], "101", contracts[0].TenantConfirmedAt!.Value.AddHours(-1));
        await Document(contracts[1], "102", contracts[1].TenantConfirmedAt!.Value.AddHours(-1));
        await Document(contracts[2], "201", contracts[2].TenantConfirmedAt!.Value.AddHours(-1));
        await Document(pending, "202", nowUtc.AddHours(-2)); // người thuê đã đối chiếu → chủ trọ có thể bấm "Xác nhận hiệu lực" khi demo

        // ---------- Chỉ số điện nước (ảnh công tơ riêng tư) và hóa đơn kèm bộ đối chiếu ----------
        (double Electric, double Water, double[] E, double[] W)[] usage =
        [
            (1200.5, 55.0, [118.3, 126.0, 109.4, 131.6], [5.2, 6.1, 4.8, 6.4]),
            (845.0, 31.0, [96.5, 102.2, 99.8], [4.1, 3.9, 4.6]),
            (2310.0, 120.0, [152.4, 165.9], [7.8, 8.4])
        ];
        var invoices = new List<(Invoice Invoice, RentalContract Contract, MeterReading Reading, bool Last, int Index)>();
        for (var k = 0; k < contracts.Count; k++)
        {
            var c = contracts[k];
            var (prevE, prevW, eUse, wUse) = usage[k];
            var firstPeriod = new DateTime(c.StartDate.Year, c.StartDate.Month, 1);
            for (var p = 0; p < eUse.Length; p++)
            {
                var month = firstPeriod.AddMonths(p);
                var period = VietnamTime.Period(month);
                var currE = Math.Round(prevE + eUse[p], 1); var currW = Math.Round(prevW + wUse[p], 1);
                var reading = new MeterReading
                {
                    RentalPostId = c.RentalPostId, Period = period, ReadingDate = Utc(month.AddMonths(1).AddDays(-1).AddHours(18).AddMinutes(30)),
                    PrevElectricIndex = prevE, CurrElectricIndex = currE, ElectricUsage = BillingCalculator.Usage(prevE, currE),
                    PrevWaterIndex = prevW, CurrWaterIndex = currW, WaterUsage = BillingCalculator.Usage(prevW, currW),
                    RecordedById = lan.Id,
                    ElectricMeterImageUrl = await MeterPhotoFiles.SaveAsync(files, Form(LandlordDemoImages.MeterPng(currE, false), "cong-to-dien.png"), ct),
                    WaterMeterImageUrl = await MeterPhotoFiles.SaveAsync(files, Form(LandlordDemoImages.MeterPng(currW, true), "dong-ho-nuoc.png"), ct)
                };
                db.MeterReadings.Add(reading);
                var services2 = new List<ServiceCharge> { new("Internet", 100000, 1, "tháng"), new("Gửi xe máy", 100000, k == 2 ? 2 : 1, "xe") };
                var breakdown = BillingCalculator.Compute(c, reading, 100000, services2, 0);
                var issue = month.AddMonths(1);
                var invoice = new Invoice
                {
                    RentalContractId = c.RentalContractId, MeterReading = reading, BillingPeriod = period,
                    RoomRentAmount = breakdown.RoomRent, ElectricUsage = breakdown.ElectricUsage, ElectricAmount = breakdown.ElectricAmount,
                    WaterUsage = breakdown.WaterUsage, WaterAmount = breakdown.WaterAmount, ServiceFeeAmount = breakdown.ServiceFee,
                    AdjustmentAmount = breakdown.Adjustment, TotalAmount = breakdown.Total, Note = DisplayFormat.Clean(breakdown.ServiceNote(), 500),
                    IssueDate = Utc(issue.AddHours(8)), DueDate = issue.AddDays(9), PaymentStatus = PaymentStatus.Paid,
                    CreatedAt = Utc(issue.AddHours(8))
                };
                var last = p == eUse.Length - 1;
                if (!last)
                {
                    invoice.TenantConfirmedAt = Utc(issue.AddDays(6).AddHours(20)); invoice.PaidAt = Utc(issue.AddDays(7).AddHours(9));
                }
                else if (k == 0) { invoice.PaymentStatus = PaymentStatus.AwaitingConfirmation; invoice.TenantConfirmedAt = nowUtc.AddDays(-1); }
                else if (k == 1) { invoice.PaymentStatus = PaymentStatus.Unpaid; invoice.DueDate = today.AddDays(-3); }
                else { invoice.PaymentStatus = PaymentStatus.Unpaid; invoice.DueDate = today.AddDays(5); }
                db.Invoices.Add(invoice);
                invoices.Add((invoice, c, reading, last, k));
                prevE = currE; prevW = currW;
            }
        }
        await db.SaveChangesAsync(ct);
        foreach (var (invoice, c, reading, _, _) in invoices)
        {
            var electric = await MeterPhotoFiles.AsFormFileAsync(files, reading.ElectricMeterImageUrl, "cong-to-dien", ct);
            var water = await MeterPhotoFiles.AsFormFileAsync(files, reading.WaterMeterImageUrl, "dong-ho-nuoc", ct);
            await documents.CaptureInvoiceAsync(invoice.InvoiceId, lan.Id, electric, water, ct);
            invoice.VietQRContent = VietQr.Build(bank.Bin, bank.AccountNumber, invoice.TotalAmount, VietQr.Description(invoice.InvoiceId, invoice.BillingPeriod));
        }
        await db.SaveChangesAsync(ct);

        // ---------- Tin nhắn với người thuê ----------
        void Chat(ApplicationUser tenant, RentalPost room, params (bool FromTenant, string Text, DateTime Utc, bool Read)[] messages)
        {
            var conversation = new Conversation
            {
                InitiatorId = tenant.Id, RecipientId = lan.Id, RentalPostId = room.RentalPostId,
                ConversationType = ConversationType.TenantLandlord, CreatedAt = messages[0].Utc, UpdatedAt = messages[^1].Utc
            };
            db.Conversations.Add(conversation);
            foreach (var m in messages)
                db.Messages.Add(new Message { Conversation = conversation, SenderId = m.FromTenant ? tenant.Id : lan.Id, Content = m.Text, SentAt = m.Utc, IsRead = m.Read });
        }
        Chat(t[4], r202,
            (true, "Chào chị, phòng 202 còn trống không ạ?", nowUtc.AddDays(-2).AddHours(-3), true),
            (false, "Chào em, phòng vẫn còn nhé. Em xem các khung giờ trống ở trang đặt lịch.", nowUtc.AddDays(-2).AddHours(-2), true),
            (true, "Giá phòng đã gồm internet chưa chị?", nowUtc.AddDays(-2).AddHours(-1), true),
            (false, "Internet 100.000 đ/tháng tính riêng em ạ, gửi xe máy 100.000 đ/xe.", nowUtc.AddDays(-2), true),
            (true, "Dạ em đặt lịch xem sáng mai 9 giờ 30 rồi ạ.", nowUtc.AddMinutes(-35), false));
        Chat(t[0], r101,
            (true, "Chị ơi, vòi nước nhà tắm phòng em bị rỉ ạ.", nowUtc.AddDays(-2).AddHours(-1), true),
            (false, "Em gửi yêu cầu sửa chữa trên hệ thống giúp chị để chị theo dõi nhé.", nowUtc.AddDays(-2), true),
            (true, "Dạ em gửi rồi ạ, em cảm ơn chị.", nowUtc.AddDays(-2).AddMinutes(20), true));
        Chat(t[1], r102,
            (false, "Chị đã gửi hóa đơn tháng này, em kiểm tra giúp chị nhé.", nowUtc.AddDays(-6), true),
            (true, "Em xin khất vài hôm, cuối tuần em chuyển khoản ạ.", nowUtc.AddHours(-6), false));
        Chat(t[3], r202,
            (false, "Chị đã tải hợp đồng lên hệ thống, em xem và bấm đã đối chiếu giúp chị.", nowUtc.AddDays(-1), true),
            (true, "Dạ em đối chiếu rồi ạ. Hẹn chị ngày dọn đến.", nowUtc.AddHours(-2), true));

        // ---------- Yêu cầu sửa chữa (đủ trạng thái, có lịch sử xử lý) ----------
        string History(params (DateTime Utc, string Text)[] lines) =>
            string.Join('\n', lines.Select(l => $"[{VietnamTime.Full(VietnamTime.ToVietnam(l.Utc))}] {l.Text}"));
        db.RepairRequests.Add(new RepairRequest
        {
            RentalPostId = r201.RentalPostId, TenantId = t[2].Id, ProblemType = "Điều hòa chảy nước", Priority = RepairPriority.Urgent,
            Description = "Điều hòa nhỏ nước xuống giường từ tối qua, em đã tắt máy và lau khô sàn.",
            ImageUrl = DemoRoomImages.RepairImage, Status = RepairRequestStatus.Pending, CreatedAt = nowUtc.AddHours(-3)
        });
        db.RepairRequests.Add(new RepairRequest
        {
            RentalPostId = r101.RentalPostId, TenantId = t[0].Id, ProblemType = "Vòi nước nhà tắm bị rỉ", Priority = RepairPriority.High,
            Description = "Vòi sen nhà tắm rỉ nước liên tục kể cả khi đã khóa.", Status = RepairRequestStatus.InProgress,
            CreatedAt = nowUtc.AddDays(-2), UpdatedAt = nowUtc.AddDays(-1),
            ResolutionNote = History((nowUtc.AddDays(-2).AddHours(1), "Tiếp nhận: Thợ hẹn đến chiều mai."),
                (nowUtc.AddDays(-1), "Đã mua vòi sen mới, chờ thợ lắp."))
        });
        db.RepairRequests.Add(new RepairRequest
        {
            RentalPostId = r102.RentalPostId, TenantId = t[1].Id, ProblemType = "Bóng đèn phòng ngủ bị cháy", Priority = RepairPriority.Medium,
            Description = "Bóng đèn trần phòng ngủ không sáng nữa.", Status = RepairRequestStatus.Resolved,
            CreatedAt = nowUtc.AddDays(-12), UpdatedAt = nowUtc.AddDays(-11), ResolvedAt = nowUtc.AddDays(-11),
            ResolutionNote = History((nowUtc.AddDays(-12).AddHours(2), "Tiếp nhận"), (nowUtc.AddDays(-11), "Đã xử lý: Đã thay bóng LED 18W mới."))
        });
        db.RepairRequests.Add(new RepairRequest
        {
            RentalPostId = r101.RentalPostId, TenantId = t[0].Id, ProblemType = "Ổ cắm cạnh bàn học bị lỏng", Priority = RepairPriority.Low,
            Description = "Ổ cắm cạnh bàn học hơi lỏng, cắm sạc hay bị tuột.", Status = RepairRequestStatus.Cancelled,
            CreatedAt = nowUtc.AddDays(-20), UpdatedAt = nowUtc.AddDays(-19)
        });

        // ---------- Yêu cầu hỗ trợ gửi quản trị viên ----------
        void Support(RentalPost? room, string subject, string category, string text, DateTime createdUtc, (string Text, DateTime Utc)? reply)
        {
            var conversation = new Conversation
            {
                InitiatorId = lan.Id, RecipientId = admin.Id, RentalPostId = room?.RentalPostId,
                ConversationType = ConversationType.LandlordAdmin, CreatedAt = createdUtc, UpdatedAt = reply?.Utc ?? createdUtc
            };
            db.Conversations.Add(conversation);
            db.Messages.Add(new Message { Conversation = conversation, SenderId = lan.Id, SentAt = createdUtc, IsRead = true,
                Content = $"{subject}\nVấn đề: {category}\n\n{text}" });
            if (reply.HasValue)
                db.Messages.Add(new Message { Conversation = conversation, SenderId = admin.Id, Content = reply.Value.Text, SentAt = reply.Value.Utc, IsRead = false });
        }
        Support(r302, "Tin phòng 302 bị yêu cầu chỉnh sửa", "Tin đăng và kiểm duyệt",
            "Tôi đã có ảnh nhà vệ sinh nhưng chưa rõ cần chụp thêm góc nào để tin được duyệt.", nowUtc.AddDays(-1),
            ("Chào chị, chị bổ sung ảnh nhà vệ sinh và chỗ để xe, chụp đủ sáng rồi gửi duyệt lại là được ạ.", nowUtc.AddHours(-20)));
        Support(null, "Hỏi về xác nhận chuyển khoản", "Hóa đơn và thanh toán",
            "Người thuê báo đã chuyển khoản nhưng tài khoản tôi chưa thấy tiền, tôi nên xử lý thế nào?", nowUtc.AddHours(-3), null);

        // ---------- Thông báo phía người thuê (đúng mẫu link của Tenant) ----------
        TenantNotifier.Add(db, t[3].Id, "Chủ nhà gửi tài liệu hợp đồng mới", "Phòng 202: phiên bản 1. Hãy xem kỹ rồi bấm đã đối chiếu.",
            TenantNotifier.ContractLink(pending.RentalContractId), "contract", nowUtc.AddDays(-1));
        var overdue = invoices.First(i => i.Last && i.Index == 1).Invoice;
        TenantNotifier.Add(db, t[1].Id, $"Hóa đơn {VietnamTime.PeriodLabel(overdue.BillingPeriod)}",
            $"Phòng 102: {DisplayFormat.Money(overdue.TotalAmount)}, đã quá hạn thanh toán.", TenantNotifier.InvoiceLink(overdue.InvoiceId), "invoice", nowUtc.AddDays(-2));
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Đã tạo dữ liệu mẫu chủ trọ: {Email} / {Password}", LandlordEmail, LandlordPassword);
    }
}
