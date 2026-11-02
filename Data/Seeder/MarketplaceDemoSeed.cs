using System.Globalization;
using System.Text.Json;
using EZ_Rent.Areas.Tenant.Services;
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
/// Phần dữ liệu mẫu thứ hai (chạy sau LandlordDemoSeed) để web trông như đang hoạt động thật:
/// - Chủ trọ thứ hai (anh Bình) ở Cầu Giấy/Đống Đa/Thanh Xuân/Hai Bà Trưng/Tây Hồ với căn hộ, nhà nguyên căn, ký túc xá…
///   có hợp đồng đang thuê (người thuê cũ của chị Lan chuyển sang), chỉ số điện nước kèm ảnh, hóa đơn kèm bộ đối chiếu.
/// - Phía người thuê: phòng yêu thích, lịch sử tìm kiếm, thông báo đủ loại, yêu cầu hỗ trợ có Admin trả lời,
///   bản nháp thông tin BM03 (tạo qua TenantContractDraftService của phía Tenant nên dấu hợp đồng luôn hợp lệ).
/// Tài khoản: chutro2.demo@ezrent.vn / Landlord@123. Có rồi thì bỏ qua.
/// </summary>
public static class MarketplaceDemoSeed
{
    public const string LandlordEmail = "chutro2.demo@ezrent.vn";

    public static async Task SeedAsync(IServiceProvider services, ILogger logger, CancellationToken ct)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var documents = services.GetRequiredService<RentalDocumentService>();
        var files = services.GetRequiredService<RentalPrivateFileStore>();
        if (await users.FindByEmailAsync(LandlordEmail) != null) return;
        var lan = await users.FindByEmailAsync(LandlordDemoSeed.LandlordEmail);
        var admin = (await users.GetUsersInRoleAsync("Admin")).OrderBy(u => u.Id).FirstOrDefault();
        if (lan == null || admin == null) return;
        var t = new List<ApplicationUser>();
        for (var i = 1; i <= 8; i++)
        {
            var user = await users.FindByEmailAsync($"nguoithue{i}.demo@ezrent.vn");
            if (user == null) return;
            t.Add(user);
        }

        var nowUtc = DateTime.UtcNow;
        var today = VietnamTime.ToVietnam(nowUtc).Date;
        var thisMonth = new DateTime(today.Year, today.Month, 1);
        static DateTime Utc(DateTime vnLocal) => DateTime.SpecifyKind(vnLocal.AddHours(-7), DateTimeKind.Utc);
        IFormFile Form(byte[] bytes, string name) => new FormFile(new MemoryStream(bytes), 0, bytes.Length, "file", name)
        { Headers = new HeaderDictionary(), ContentType = name.EndsWith(".pdf") ? "application/pdf" : "image/png" };

        // ---------- Chủ trọ thứ hai ----------
        var binh = new ApplicationUser
        {
            UserName = LandlordEmail, Email = LandlordEmail, FullName = "Trần Văn Bình", PhoneNumber = "0987654321",
            EmailConfirmed = true, Status = UserStatus.Active, CreatedAt = Utc(thisMonth.AddMonths(-8))
        };
        var created = await users.CreateAsync(binh, LandlordDemoSeed.LandlordPassword);
        if (!created.Succeeded) throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));
        await users.AddToRoleAsync(binh, "Landlord");
        var bank = new BankAccount("970407", "19034567890011", "TRAN VAN BINH");
        await BankAccountStore.SaveAsync(db, binh.Id, bank, ct);
        var hours = new ViewingHours();
        for (var d = 1; d <= 5; d++) hours.Weekly[d] = ["18:00", "19:30"];
        hours.Weekly[0] = ["09:00", "10:30", "15:00"]; hours.Weekly[6] = ["09:00", "10:30", "15:00"];
        await ViewingHoursStore.SaveAsync(db, binh.Id, hours, ct);
        await LandlordLegalProfileStore.SaveAsync(db, binh.Id, new LandlordLegalProfile(new DateOnly(1979, 9, 2),
            IdentityDocumentKind.CitizenIdentity, "001079034567", new DateOnly(2022, 3, 15), "Cục Cảnh sát QLHC về trật tự xã hội",
            "Số 8 ngõ 165 Cầu Giấy, Hà Nội"), ct);

        RentalPost Room(string seed, string title, RoomType type, decimal price, double area, string address, string ward, string district,
            string description, string[] amenities, ApprovalStatus approval, RoomStatus status, int daysOld)
        {
            var createdUtc = nowUtc.AddDays(-daysOld);
            var post = new RentalPost
            {
                Title = title, Description = description, Price = price, Area = area, RoomType = type, Address = address, Ward = ward,
                District = district, City = "Hà Nội", AmenitiesJson = JsonSerializer.Serialize(amenities), ElectricityUnitPrice = 3800,
                WaterUnitPrice = 30000, ServiceFee = 150000, ApprovalStatus = approval, RoomStatus = status, OwnerId = binh.Id,
                AvailableFrom = today.AddDays(3), CreatedAt = createdUtc, UpdatedAt = createdUtc
            };
            if (approval == ApprovalStatus.Approved) { post.ApprovedById = admin.Id; post.ApprovedAt = createdUtc.AddHours(20); post.PostedAt = post.ApprovedAt; }
            for (var i = 0; i < 4; i++)
                post.RoomImages.Add(new RoomImage { MediaUrl = DemoRoomImages.For(type, seed.Sum(ch => ch), i, title.Contains("gác")), IsPrimary = i == 0, DisplayOrder = i, CreatedAt = createdUtc });
            post.Services.Add(new Service { Name = "Internet", UnitPrice = 120000, Unit = "tháng" });
            post.Services.Add(new Service { Name = "Gửi xe máy", UnitPrice = 120000, Unit = "xe" });
            db.RentalPosts.Add(post);
            return post;
        }
        var apartment = Room("binh-ch1", "Căn hộ 1 ngủ full đồ, thang máy · Cầu Giấy", RoomType.Apartment, 6_500_000, 40, "Số 8 ngõ 165 Cầu Giấy", "Quan Hoa", "Cầu Giấy",
            "Căn hộ 40 m² có phòng ngủ riêng, bếp, máy giặt riêng, thang máy và bảo vệ 24/7. Gần công viên Cầu Giấy và nhiều trường đại học.",
            ["Wifi", "Điều hòa", "Nóng lạnh", "Máy giặt", "Tủ lạnh", "Bếp riêng", "WC khép kín", "Thang máy", "Camera an ninh"], ApprovalStatus.Approved, RoomStatus.Rented, 200);
        var house = Room("binh-nha", "Nhà nguyên căn 3 tầng, 3 phòng ngủ · Đống Đa", RoomType.House, 12_000_000, 60, "Số 21 ngõ 102 Trường Chinh", "Phương Mai", "Đống Đa",
            "Nhà 3 tầng mỗi tầng 20 m², 3 phòng ngủ, 2 WC, sân để xe trước nhà. Phù hợp nhóm bạn hoặc gia đình, giờ giấc tự do.",
            ["Wifi", "Điều hòa", "Nóng lạnh", "Bếp riêng", "Chỗ để xe", "Giờ giấc tự do", "Không chung chủ"], ApprovalStatus.Approved, RoomStatus.Available, 40);
        var dorm = Room("binh-ktx", "Ký túc xá giường tầng sạch sẽ · gần ĐH Bách khoa", RoomType.Dormitory, 1_200_000, 12, "Số 5 ngõ 40 Tạ Quang Bửu", "Bách Khoa", "Hai Bà Trưng",
            "Giường tầng trong phòng 6 người, có tủ đồ riêng có khóa, điều hòa, máy giặt chung. Phù hợp sinh viên năm nhất, giá đã gồm điện nước sinh hoạt cơ bản.",
            ["Wifi", "Điều hòa", "Máy giặt", "Camera an ninh", "Chỗ để xe"], ApprovalStatus.Approved, RoomStatus.Available, 25);
        var tx = Room("binh-tx", "Phòng 18 m² cửa sổ thoáng · Thanh Xuân", RoomType.Room, 2_400_000, 18, "Số 12 ngõ 214 Nguyễn Xiển", "Hạ Đình", "Thanh Xuân",
            "Phòng 18 m² có cửa sổ, WC khép kín, nóng lạnh, gần bến xe buýt và Royal City. Chủ nhà thân thiện, an ninh tốt.",
            ["Wifi", "Nóng lạnh", "WC khép kín", "Chỗ để xe", "Giờ giấc tự do"], ApprovalStatus.Approved, RoomStatus.Available, 15);
        Room("binh-th", "Studio ban công hướng hồ · Tây Hồ", RoomType.Room, 5_200_000, 30, "Số 3 ngõ 52 Tô Ngọc Vân", "Quảng An", "Tây Hồ",
            "Studio 30 m² ban công nhìn ra hồ Tây, nội thất mới, máy giặt riêng. Đang chờ quản trị viên duyệt tin.",
            ["Wifi", "Điều hòa", "Nóng lạnh", "Máy giặt", "Ban công", "WC khép kín"], ApprovalStatus.Pending, RoomStatus.Available, 1);
        await db.SaveChangesAsync(ct);

        // ---------- Hợp đồng đang thuê: người thuê cũ của chị Lan chuyển sang căn hộ ----------
        var start = thisMonth.AddMonths(-3);
        var contract = new RentalContract
        {
            RentalPostId = apartment.RentalPostId, TenantId = t[7].Id, LandlordId = binh.Id, StartDate = start, EndDate = start.AddMonths(12).AddDays(-1),
            MonthlyRent = apartment.Price, DepositAmount = apartment.Price * 2, AgreedElectricityPrice = 3800, AgreedWaterPrice = 30000,
            Terms = ContractTermsHeader.Compose("transfer", "Hà Nội", "Điều 1. Bên B sử dụng căn hộ để ở, giữ gìn nội thất theo biên bản bàn giao.\nĐiều 2. Thanh toán trước ngày 05 hằng tháng qua mã VietQR trên hóa đơn.\nĐiều 3. Báo trước 30 ngày nếu chấm dứt hợp đồng trước hạn."),
            Status = ContractStatus.Active, LandlordConfirmedAt = Utc(start.AddDays(-2).AddHours(10)), TenantConfirmedAt = Utc(start.AddDays(-2).AddHours(10)),
            CreatedAt = Utc(start.AddDays(-5).AddHours(9))
        };
        db.RentalContracts.Add(contract);
        db.ViewingAppointments.Add(new ViewingAppointment { RentalPostId = apartment.RentalPostId, TenantId = t[7].Id, ViewingDate = start.AddDays(-7).AddHours(18),
            Status = ViewingAppointmentStatus.Completed, Note = "Em muốn xem căn hộ sau giờ làm.", LandlordNote = "Khách đã xem, chốt thuê.", CreatedAt = Utc(start.AddDays(-9)) });
        await db.SaveChangesAsync(ct);
        var doc = await documents.AppendContractAsync(contract.RentalContractId, binh.Id, 0, Form(LandlordDemoImages.ContractPdf(
        [
            "HOP DONG THUE CAN HO", $"So {contract.RentalContractId}/EZ-RENT - So 8 ngo 165 Cau Giay, Ha Noi",
            "Ben A: Tran Van Binh - 0987654321", $"Ben B: {DisplayFormat.Ascii(t[7].FullName)} - {t[7].PhoneNumber}",
            $"Thoi han: {contract.StartDate:dd/MM/yyyy} - {contract.EndDate:dd/MM/yyyy}", "Tien thue 6.500.000 d/thang - Dat coc 13.000.000 d"
        ]), "hop-dong-can-ho.pdf"), ct);
        await db.ContractDocuments.Where(d => d.ContractDocumentId == doc.ContractDocumentId)
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.TenantAcknowledgedAt, contract.TenantConfirmedAt!.Value.AddHours(-1)), ct);

        double prevE = 5120.0, prevW = 210.0;
        double[] eUse = [168.2, 175.5, 160.9], wUse = [9.1, 8.7, 9.6];
        var bills = new List<(Invoice Invoice, MeterReading Reading)>();
        for (var p = 0; p < eUse.Length; p++)
        {
            var month = start.AddMonths(p);
            var currE = Math.Round(prevE + eUse[p], 1); var currW = Math.Round(prevW + wUse[p], 1);
            var reading = new MeterReading
            {
                RentalPostId = apartment.RentalPostId, Period = VietnamTime.Period(month), ReadingDate = Utc(month.AddMonths(1).AddDays(-1).AddHours(19)),
                PrevElectricIndex = prevE, CurrElectricIndex = currE, ElectricUsage = BillingCalculator.Usage(prevE, currE),
                PrevWaterIndex = prevW, CurrWaterIndex = currW, WaterUsage = BillingCalculator.Usage(prevW, currW), RecordedById = binh.Id,
                ElectricMeterImageUrl = await MeterPhotoFiles.SaveAsync(files, Form(LandlordDemoImages.MeterPng(currE, false), "cong-to-dien.png"), ct),
                WaterMeterImageUrl = await MeterPhotoFiles.SaveAsync(files, Form(LandlordDemoImages.MeterPng(currW, true), "dong-ho-nuoc.png"), ct)
            };
            db.MeterReadings.Add(reading);
            var b = BillingCalculator.Compute(contract, reading, 150000, [new("Internet", 120000, 1, "tháng"), new("Gửi xe máy", 120000, 2, "xe")], 0);
            var issue = month.AddMonths(1);
            var last = p == eUse.Length - 1;
            var invoice = new Invoice
            {
                RentalContractId = contract.RentalContractId, MeterReading = reading, BillingPeriod = reading.Period, RoomRentAmount = b.RoomRent,
                ElectricUsage = b.ElectricUsage, ElectricAmount = b.ElectricAmount, WaterUsage = b.WaterUsage, WaterAmount = b.WaterAmount,
                ServiceFeeAmount = b.ServiceFee, AdjustmentAmount = b.Adjustment, TotalAmount = b.Total, Note = DisplayFormat.Clean(b.ServiceNote(), 500),
                IssueDate = Utc(issue.AddHours(8)), DueDate = last ? today.AddDays(4) : issue.AddDays(4), CreatedAt = Utc(issue.AddHours(8)),
                PaymentStatus = last ? PaymentStatus.Unpaid : PaymentStatus.Paid,
                TenantConfirmedAt = last ? null : Utc(issue.AddDays(2).AddHours(21)), PaidAt = last ? null : Utc(issue.AddDays(3).AddHours(8))
            };
            db.Invoices.Add(invoice);
            bills.Add((invoice, reading));
            prevE = currE; prevW = currW;
        }
        await db.SaveChangesAsync(ct);
        foreach (var (invoice, reading) in bills)
        {
            await documents.CaptureInvoiceAsync(invoice.InvoiceId, binh.Id,
                await MeterPhotoFiles.AsFormFileAsync(files, reading.ElectricMeterImageUrl, "cong-to-dien", ct),
                await MeterPhotoFiles.AsFormFileAsync(files, reading.WaterMeterImageUrl, "dong-ho-nuoc", ct), ct);
            invoice.VietQRContent = VietQr.Build(bank.Bin, bank.AccountNumber, invoice.TotalAmount, VietQr.Description(invoice.InvoiceId, invoice.BillingPeriod));
        }

        // ---------- Lịch xem, tin nhắn, sửa chữa với chủ trọ thứ hai ----------
        void Viewing(RentalPost room, ApplicationUser tenant, DateTime vn, ViewingAppointmentStatus status, DateTime createdUtc, string note, string? reply) =>
            db.ViewingAppointments.Add(new ViewingAppointment { RentalPostId = room.RentalPostId, TenantId = tenant.Id, ViewingDate = vn, Status = status, Note = note, LandlordNote = reply, CreatedAt = createdUtc });
        Viewing(house, t[4], today.AddDays(1).AddHours(18), ViewingAppointmentStatus.Pending, nowUtc.AddHours(-1), "Nhóm em 3 người muốn xem nhà buổi tối ạ.", null);
        Viewing(dorm, t[5], today.AddDays(2).AddHours(9), ViewingAppointmentStatus.Confirmed, nowUtc.AddHours(-26), "Em là tân sinh viên Bách khoa.", "Đã xác nhận, em mang theo thẻ sinh viên nhé.");
        Viewing(tx, t[6], today.AddDays(-5).AddHours(18), ViewingAppointmentStatus.Completed, Utc(today.AddDays(-6).AddHours(9)), "Cho em xem phòng sau 18 giờ.", "Đã dẫn khách xem phòng.");
        Viewing(tx, t[0], today.AddDays(-9).AddHours(19).AddMinutes(30), ViewingAppointmentStatus.Cancelled, Utc(today.AddDays(-10).AddHours(8)), "Em xem giúp bạn cùng lớp.", null);
        void Chat(ApplicationUser tenant, RentalPost room, params (bool FromTenant, string Text, DateTime Utc, bool Read)[] messages)
        {
            var conversation = new Conversation { InitiatorId = tenant.Id, RecipientId = binh.Id, RentalPostId = room.RentalPostId,
                ConversationType = ConversationType.TenantLandlord, CreatedAt = messages[0].Utc, UpdatedAt = messages[^1].Utc };
            db.Conversations.Add(conversation);
            foreach (var m in messages)
                db.Messages.Add(new Message { Conversation = conversation, SenderId = m.FromTenant ? tenant.Id : binh.Id, Content = m.Text, SentAt = m.Utc, IsRead = m.Read });
        }
        Chat(t[5], dorm, (true, "Anh ơi ký túc xá còn giường tầng dưới không ạ?", nowUtc.AddDays(-2), true),
            (false, "Còn 1 giường tầng dưới em nhé, em đặt lịch xem sáng thứ bảy được không?", nowUtc.AddDays(-2).AddHours(1), true),
            (true, "Dạ em đặt rồi ạ, em cảm ơn anh.", nowUtc.AddHours(-26), true));
        Chat(t[7], apartment, (false, "Hóa đơn tháng này anh gửi rồi nhé, hạn trước ngày 05.", nowUtc.AddDays(-1), true),
            (true, "Dạ em nhận được rồi, cuối tuần em chuyển ạ.", nowUtc.AddHours(-3), false));
        Chat(t[4], house, (true, "Nhà còn cho thuê không anh, bọn em 3 người là sinh viên năm cuối.", nowUtc.AddHours(-2), false));
        db.RepairRequests.Add(new RepairRequest
        {
            RentalPostId = apartment.RentalPostId, TenantId = t[7].Id, ProblemType = "Máy giặt kêu to khi vắt", Priority = RepairPriority.Medium,
            Description = "Máy giặt rung mạnh và kêu to ở chế độ vắt, em đã kê lại chân máy nhưng chưa đỡ.", Status = RepairRequestStatus.InProgress,
            CreatedAt = nowUtc.AddDays(-3), UpdatedAt = nowUtc.AddDays(-2),
            ResolutionNote = $"[{VietnamTime.Full(VietnamTime.ToVietnam(nowUtc.AddDays(-2)))}] Tiếp nhận: Hẹn thợ kiểm tra bộ giảm chấn vào thứ bảy."
        });

        // ---------- Dữ liệu phía người thuê ----------
        var others = await db.RentalPosts.AsNoTracking()
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved && p.RoomStatus == RoomStatus.Available && p.OwnerId != lan.Id && p.OwnerId != binh.Id)
            .OrderBy(p => p.RentalPostId).Select(p => p.RentalPostId).Take(6).ToListAsync(ct);
        var favorites = new List<(ApplicationUser User, int RoomId)> { (t[0], dorm.RentalPostId), (t[0], tx.RentalPostId), (t[4], house.RentalPostId),
            (t[5], dorm.RentalPostId), (t[6], tx.RentalPostId), (t[6], house.RentalPostId) };
        for (var i = 0; i < others.Count; i++) favorites.Add((t[4 + i % 3], others[i]));
        foreach (var (user, roomId) in favorites.Distinct())
            if (!await db.Favorites.AnyAsync(f => f.UserId == user.Id && f.RentalPostId == roomId, ct))
                db.Favorites.Add(new Favorite { UserId = user.Id, RentalPostId = roomId, CreatedAt = nowUtc.AddDays(-Random.Shared.Next(1, 20)) });
        var searches = new (ApplicationUser User, string District, decimal Min, decimal Max, RoomType Type)[]
        {
            (t[4], "Hoàng Mai", 2_000_000, 3_000_000, RoomType.Room), (t[4], "Đống Đa", 8_000_000, 15_000_000, RoomType.House),
            (t[5], "Hai Bà Trưng", 800_000, 2_000_000, RoomType.Dormitory), (t[6], "Thanh Xuân", 2_000_000, 3_500_000, RoomType.Room),
            (t[3], "Hoàng Mai", 2_000_000, 3_000_000, RoomType.Room), (t[0], "Cầu Giấy", 3_000_000, 7_000_000, RoomType.Apartment)
        };
        for (var i = 0; i < searches.Length; i++)
            db.SearchHistories.Add(new SearchHistory { UserId = searches[i].User.Id, City = "Hà Nội", District = searches[i].District, MinPrice = searches[i].Min,
                MaxPrice = searches[i].Max, RoomType = searches[i].Type, SearchedAt = nowUtc.AddHours(-6 * (i + 1)) });

        void Support(ApplicationUser tenant, string subject, string category, string text, DateTime createdUtc, (string Text, DateTime Utc)? reply)
        {
            var conversation = new Conversation { InitiatorId = tenant.Id, RecipientId = admin.Id, ConversationType = ConversationType.TenantAdmin,
                CreatedAt = createdUtc, UpdatedAt = reply?.Utc ?? createdUtc };
            db.Conversations.Add(conversation);
            db.Messages.Add(new Message { Conversation = conversation, SenderId = tenant.Id, SentAt = createdUtc, IsRead = true, Content = $"{subject}\nVấn đề: {category}\n\n{text}" });
            if (reply.HasValue) db.Messages.Add(new Message { Conversation = conversation, SenderId = admin.Id, Content = reply.Value.Text, SentAt = reply.Value.Utc, IsRead = false });
        }
        Support(t[0], "Hỏi về xác nhận hợp đồng trên web", "Vấn đề khác", "Em đã đối chiếu tài liệu hợp đồng, như vậy đã đủ để hợp đồng có hiệu lực chưa ạ?",
            nowUtc.AddDays(-4), ("Chào bạn, sau khi bạn đối chiếu, chủ trọ xác nhận là hợp đồng có hiệu lực. Bạn theo dõi trạng thái ở mục Hợp đồng nhé.", nowUtc.AddDays(-4).AddHours(3)));
        Support(t[4], "Không thấy khung giờ buổi tối", "Vấn đề khi xem phòng", "Phòng 202 em muốn xem buổi tối nhưng chỉ thấy một khung 19:00.", nowUtc.AddHours(-5), null);

        void Notify(ApplicationUser user, string title, string content, string? link, string type, DateTime atUtc, bool read) =>
            db.Notifications.Add(new Notification { UserId = user.Id, Title = title, Content = content, Link = link, Type = type, IsRead = read, CreatedAt = atUtc });
        Notify(t[0], "Hóa đơn đã được xác nhận thanh toán", "Phòng 101: chủ nhà đã xác nhận nhận tiền kỳ trước.", null, "invoice", nowUtc.AddDays(-20), true);
        Notify(t[0], "Yêu cầu sửa chữa đang được xử lý", "Vòi nước nhà tắm: chủ nhà đã tiếp nhận và hẹn thợ.", null, "repair", nowUtc.AddDays(-2), false);
        Notify(t[2], "Đã nhận yêu cầu sửa chữa", "Điều hòa chảy nước: yêu cầu của bạn đang chờ chủ nhà tiếp nhận.", null, "repair", nowUtc.AddHours(-3), false);
        Notify(t[5], "Lịch xem phòng đã được xác nhận", "Ký túc xá gần ĐH Bách khoa: hẹn 09:00 ngày kia.", null, "viewing", nowUtc.AddHours(-25), false);
        Notify(t[5], "Lịch xem phòng bị từ chối", "Phòng 202: chủ nhà bận vào giờ bạn chọn, hãy chọn khung khác.", null, "viewing", nowUtc.AddDays(-4), true);
        Notify(t[6], "Lịch xem phòng đã được xác nhận", "Phòng 202: hẹn 17:30 ngày mai.", null, "viewing", nowUtc.AddHours(-20), false);
        Notify(t[7], "Hóa đơn mới", $"Căn hộ Cầu Giấy: {DisplayFormat.Money(bills[^1].Invoice.TotalAmount)}, hạn thanh toán {VietnamTime.Date(bills[^1].Invoice.DueDate)}.",
            TenantNotifier.InvoiceLink(bills[^1].Invoice.InvoiceId), "invoice", nowUtc.AddDays(-1), false);
        await db.SaveChangesAsync(ct);

        // ---------- Bản nháp BM03 của người thuê đang chờ hợp đồng phòng 202 (qua dịch vụ của phía Tenant) ----------
        var pendingId = await db.RentalContracts.Where(c => c.TenantId == t[3].Id && c.Status == ContractStatus.PendingTenant)
            .Select(c => (int?)c.RentalContractId).FirstOrDefaultAsync(ct);
        if (pendingId.HasValue)
        {
            var drafts = services.GetRequiredService<TenantContractDraftService>();
            var page = await drafts.GetAsync(pendingId.Value, t[3].Id, ct: ct);
            if (page != null && page.LatestRevision == 0)
            {
                var input = page.Input;
                input.FullName = t[3].FullName; input.PhoneNumber = t[3].PhoneNumber; input.DateOfBirth = new DateOnly(2003, 8, 19);
                input.PermanentAddress = "Xóm 4, xã Nghi Phú, TP Vinh, Nghệ An"; input.DocumentKind = IdentityDocumentKind.CitizenIdentity;
                input.DocumentNumber = "040203001234"; input.DocumentIssuedOn = new DateOnly(2021, 7, 12); input.DocumentIssuedAt = "Cục Cảnh sát QLHC về trật tự xã hội";
                await drafts.SaveAsync(pendingId.Value, t[3].Id, input, ct);
            }
        }
        logger.LogInformation("Đã tạo dữ liệu mẫu thứ hai: chủ trọ {Email} / {Password}", LandlordEmail, LandlordDemoSeed.LandlordPassword);
    }
}
