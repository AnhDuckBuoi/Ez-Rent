using System.Text.Encodings.Web;
using System.Text.Json;
using Bogus;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EZ_Rent.Data.Seeder;

public static class DbSeeder
{
    // ================== CẤU HÌNH SỐ LƯỢNG ==================
    private const int LandlordCount = 5;
    private const int TenantCount = 15;
    private const int PostsPerLandlord = 12;    // ← 5 × 12 = 60 posts
    private const int ImagesPerPost = 10;
    private const int ServicesPerPost = 3;

    // Seed cho MỌI random của mình (Bogus + _rng) → data cố định 100%
    private const int SeedValue = 20250101;
    private static Random _rng = new(SeedValue);

    // ================== DỮ LIỆU MẪU ==================
    private static readonly (string City, string District, string Ward)[] Locations =
    {
        ("Hà Nội",             "Cầu Giấy",    "Dịch Vọng Hậu"),
        ("Hà Nội",             "Đống Đa",     "Láng Hạ"),
        ("Hà Nội",             "Thanh Xuân",  "Khương Trung"),
        ("Hà Nội",             "Hai Bà Trưng","Bách Khoa"),
        ("Hà Nội",             "Ba Đình",     "Kim Mã"),
        ("TP. Hồ Chí Minh",    "Quận 1",      "Bến Nghé"),
        ("TP. Hồ Chí Minh",    "Quận 3",      "Võ Thị Sáu"),
        ("TP. Hồ Chí Minh",    "Quận 7",      "Tân Phú"),
        ("TP. Hồ Chí Minh",    "Bình Thạnh",  "Phường 25"),
        ("TP. Hồ Chí Minh",    "Phú Nhuận",   "Phường 7"),
        ("Đà Nẵng",            "Hải Châu",    "Thạch Thang"),
        ("Đà Nẵng",            "Sơn Trà",     "An Hải Bắc"),
        ("Cần Thơ",            "Ninh Kiều",   "An Khánh"),
    };

    private static readonly string[] Streets =
    {
        "Nguyễn Trãi", "Lê Lợi", "Trần Hưng Đạo", "Hai Bà Trưng",
        "Võ Văn Tần", "Phan Xích Long", "Nguyễn Văn Cừ", "Lý Thường Kiệt",
        "Nguyễn Huệ", "Điện Biên Phủ", "Cách Mạng Tháng 8", "Pasteur",
        "Xô Viết Nghệ Tĩnh", "Hoàng Diệu", "Lê Văn Việt"
    };

    private static readonly string[] Amenities =
        { "Wifi", "Điều hòa", "Nóng lạnh", "Tủ lạnh", "Máy giặt",
          "Ban công", "Thang máy", "Chỗ đậu xe", "Bếp riêng", "Camera an ninh" };

    private static readonly string[] ServiceNames =
        { "Phí vệ sinh", "Internet", "Gửi xe máy", "Nước sinh hoạt", "Điện chung" };

    private static readonly string[] ProblemTypes =
        { "Điện", "Nước", "Điều hòa", "Tủ lạnh", "Máy giặt",
          "Khóa cửa", "Nhà vệ sinh", "Thấm dột" };

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // ================== ENTRY POINT ==================
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        Randomizer.Seed = new Random(SeedValue);
        _rng = new Random(SeedValue);

        // 1. Roles + Admin
        await SeedRolesAsync(roleManager);
        var admin = await SeedAdminAsync(userManager);

        // 2. Nếu đã có RentalPost → bỏ qua
        if (await db.RentalPosts.AnyAsync())
        {
            Console.WriteLine("⏭  Đã có data, bỏ qua seed.");
            return;
        }

        Console.WriteLine("🌱 Bắt đầu seed data...");

        var (landlords, tenants) = await SeedUsersAsync(userManager);
        var posts = await SeedRentalPostsAsync(db, landlords);

        await SeedInteractionsAsync(db, posts, tenants);
        await SeedContractsAndInvoicesAsync(db, posts, tenants);
        await SeedRepairsAsync(db, tenants);
        await SeedNotificationsAsync(db, tenants, landlords, admin);

        Console.WriteLine("✅ Seed data hoàn tất!");
    }

    // ================================================================
    // 1. ROLES
    // ================================================================
    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        string[] roles = { "Tenant", "Landlord", "Admin" };
        foreach (var role in roles)
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
    }

    // ================================================================
    // 2. ADMIN
    // ================================================================
    private static async Task<ApplicationUser> SeedAdminAsync(UserManager<ApplicationUser> userManager)
    {
        var admin = await userManager.FindByEmailAsync("admin@gmail.com");
        if (admin != null) return admin;

        admin = new ApplicationUser
        {
            UserName = "admin@gmail.com",
            Email = "admin@gmail.com",
            EmailConfirmed = true,
            FullName = "Quản trị viên",
            AvatarUrl = "https://i.pravatar.cc/200?img=68",
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, "Admin@123");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");

        return admin;
    }

    // ================================================================
    // 3. LANDLORDS + TENANTS
    // ================================================================
    private static async Task<(List<ApplicationUser> Landlords, List<ApplicationUser> Tenants)>
        SeedUsersAsync(UserManager<ApplicationUser> userManager)
    {
        var landlords = new List<ApplicationUser>();
        var tenants = new List<ApplicationUser>();

        var userFaker = new Faker<ApplicationUser>("vi")
            .RuleFor(u => u.FullName, f => f.Name.FullName())
            .RuleFor(u => u.AvatarUrl, f => f.Internet.Avatar())
            .RuleFor(u => u.PhoneNumber, f => f.Phone.PhoneNumber("0*********"))
            .RuleFor(u => u.Status, _ => UserStatus.Active)
            .RuleFor(u => u.EmailConfirmed, _ => true)
            .RuleFor(u => u.CreatedAt, f => f.Date.Past(2));

        for (int i = 0; i < LandlordCount; i++)
        {
            var u = userFaker.Generate();
            u.UserName = $"landlord{i + 1}@ezrent.vn";
            u.Email = u.UserName;

            var r = await userManager.CreateAsync(u, "Landlord@123");
            if (r.Succeeded)
            {
                await userManager.AddToRoleAsync(u, "Landlord");
                landlords.Add(u);
            }
        }

        for (int i = 0; i < TenantCount; i++)
        {
            var u = userFaker.Generate();
            u.UserName = $"tenant{i + 1}@ezrent.vn";
            u.Email = u.UserName;

            var r = await userManager.CreateAsync(u, "Tenant@123");
            if (r.Succeeded)
            {
                await userManager.AddToRoleAsync(u, "Tenant");
                tenants.Add(u);
            }
        }

        Console.WriteLine($"   → {landlords.Count} landlords, {tenants.Count} tenants");
        return (landlords, tenants);
    }

    // ================================================================
    // 4. RENTAL POSTS + IMAGES + SERVICES
    //     Mỗi landlord chuyên 1 khu vực + khoảng giá → tạo cluster
    // ================================================================
    private static async Task<List<RentalPost>> SeedRentalPostsAsync(
        AppDbContext db, List<ApplicationUser> landlords)
    {
        var f = new Faker("vi");
        var posts = new List<RentalPost>();

        // Lọc locations theo City
        var hnLocations = Locations.Where(l => l.City == "Hà Nội").ToArray();
        var hcmLocations = Locations.Where(l => l.City == "TP. Hồ Chí Minh").ToArray();
        var dnLocations = Locations.Where(l => l.City == "Đà Nẵng").ToArray();
        var ctLocations = Locations.Where(l => l.City == "Cần Thơ").ToArray();

        // Config cho từng landlord
        var landlordConfigs = new[]
        {
            // landlord1: HN giá rẻ
            (locations: hnLocations, minPrice: 1_500_000m, maxPrice: 5_000_000m),
            // landlord2: HN giá rẻ
            (locations: hnLocations, minPrice: 1_500_000m, maxPrice: 5_000_000m),
            // landlord3: HCM cao cấp
            (locations: hcmLocations, minPrice: 6_000_000m, maxPrice: 14_000_000m),
            // landlord4: HCM cao cấp
            (locations: hcmLocations, minPrice: 6_000_000m, maxPrice: 14_000_000m),
            // landlord5: Đà Nẵng + Cần Thơ (random)
            (locations: dnLocations.Concat(ctLocations).ToArray(), minPrice: 1_500_000m, maxPrice: 8_000_000m),
        };

        for (int li = 0; li < landlords.Count && li < landlordConfigs.Length; li++)
        {
            var landlord = landlords[li];
            var config = landlordConfigs[li];

            for (int i = 0; i < PostsPerLandlord; i++)
            {
                var loc = config.locations[_rng.Next(config.locations.Length)];
                var createdAt = f.Date.Past(6);

                var price = Math.Round(
                    f.Random.Decimal(config.minPrice, config.maxPrice) / 100_000) * 100_000;

                var post = new RentalPost
                {
                    OwnerId = landlord.Id,

                    Title = $"Phòng {f.PickRandom("trọ", "cho thuê", "studio", "trọ cao cấp")} " +
                            $"{f.PickRandom("gần trung tâm", "gần đại học", "gần chợ", "gần bến xe", "khu an ninh")}",

                    Description = f.Lorem.Paragraphs(3, "\n\n"),

                    Price = price,
                    Area = Math.Round(f.Random.Double(15, 80), 1),

                    Address = $"{_rng.Next(1, 300)} {f.PickRandom(Streets)}",
                    City = loc.City,
                    District = loc.District,
                    Ward = loc.Ward,
                    MapLocation = $"https://maps.google.com/?q={f.Random.Double(10, 21)},{f.Random.Double(105, 110)}",

                    RoomType = f.PickRandom<RoomType>(),
                    ElectricityUnitPrice = f.PickRandom(3000m, 3500m, 3800m, 4000m),
                    WaterUnitPrice = f.PickRandom(15000m, 20000m, 25000m),
                    ServiceFee = f.PickRandom(100_000m, 150_000m, 200_000m),

                    AvailableFrom = f.Date.Soon(30),
                    CreatedAt = createdAt,

                    AmenitiesJson = JsonSerializer.Serialize(
                        Amenities.OrderBy(_ => _rng.Next()).Take(_rng.Next(4, 8)),
                        JsonOpts)
                };

                // 80% Approved
                var roll = _rng.Next(100);
                if (roll < 80)
                {
                    post.ApprovalStatus = ApprovalStatus.Approved;
                    post.RoomStatus = RoomStatus.Available;
                    post.PostedAt = createdAt.AddHours(_rng.Next(1, 48));
                }
                else if (roll < 90)
                {
                    post.ApprovalStatus = ApprovalStatus.Pending;
                    post.RoomStatus = RoomStatus.Hidden;
                }
                else if (roll < 97)
                {
                    post.ApprovalStatus = ApprovalStatus.Draft;
                    post.RoomStatus = RoomStatus.Hidden;
                }
                else
                {
                    post.ApprovalStatus = ApprovalStatus.Rejected;
                    post.RoomStatus = RoomStatus.Hidden;
                    post.ApprovalNote = "Hình ảnh chưa rõ ràng, vui lòng bổ sung thêm.";
                }

                posts.Add(post);
            }
        }

        db.RentalPosts.AddRange(posts);
        await db.SaveChangesAsync();

        // ----- Images + Services -----
        var images = new List<RoomImage>();
        var services = new List<Service>();

        foreach (var post in posts)
        {
            for (int i = 0; i < ImagesPerPost; i++)
            {
                images.Add(new RoomImage
                {
                    RentalPostId = post.RentalPostId,
                    MediaUrl = $"https://picsum.photos/seed/room{post.RentalPostId}_{i}/800/600",
                    MediaType = MediaType.Image,
                    IsPrimary = i == 0,
                    DisplayOrder = i,
                    CreatedAt = post.CreatedAt
                });
            }

            var pickedServices = ServiceNames
                .OrderBy(_ => _rng.Next())
                .Take(ServicesPerPost);

            foreach (var name in pickedServices)
            {
                services.Add(new Service
                {
                    RentalPostId = post.RentalPostId,
                    Name = name,
                    UnitPrice = name switch
                    {
                        "Phí vệ sinh" => 50_000m,
                        "Internet" => 100_000m,
                        "Gửi xe máy" => 100_000m,
                        _ => 80_000m
                    },
                    Unit = "tháng",
                    Description = $"{name} hàng tháng"
                });
            }
        }

        db.RoomImages.AddRange(images);
        db.Services.AddRange(services);
        await db.SaveChangesAsync();

        Console.WriteLine($"   → {posts.Count} bài đăng, {images.Count} ảnh, {services.Count} dịch vụ");
        return posts;
    }

    // ================================================================
    // 5. FAVORITES / REQUESTS / VIEWINGS / CONVERSATIONS / MESSAGES
    // ================================================================
    // ================================================================
    // 5. FAVORITES / REQUESTS / VIEWINGS / CONVERSATIONS / MESSAGES
    //    TĂNG SỐ LƯỢNG để cải thiện chất lượng model
    // ================================================================
    private static async Task SeedInteractionsAsync(
        AppDbContext db, List<RentalPost> posts, List<ApplicationUser> tenants)
    {
        var approvedPosts = posts
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
            .ToList();
        if (approvedPosts.Count == 0) return;

        // Ngưỡng giá khớp với config của landlord
        var hnCheapPosts = approvedPosts
            .Where(p => p.City == "Hà Nội" && p.Price <= 5_000_000m)
            .ToList();

        var hcmPremiumPosts = approvedPosts
            .Where(p => p.City == "TP. Hồ Chí Minh" && p.Price >= 6_000_000m)
            .ToList();

        if (hnCheapPosts.Count == 0) hnCheapPosts = approvedPosts;
        if (hcmPremiumPosts.Count == 0) hcmPremiumPosts = approvedPosts;

        var groupA = tenants.Take(5).ToList();          // HN giá rẻ
        var groupB = tenants.Skip(5).Take(5).ToList();  // HCM cao cấp

        List<RentalPost> GetPreferredPosts(ApplicationUser tenant)
        {
            if (groupA.Contains(tenant)) return hnCheapPosts;
            if (groupB.Contains(tenant)) return hcmPremiumPosts;
            return approvedPosts;
        }

        // ---------- FAVORITES: 10-15 per tenant ----------
        var favKeys = new HashSet<(string, int)>();
        var favorites = new List<Favorite>();

        foreach (var tenant in tenants)
        {
            var pool = GetPreferredPosts(tenant);
            int target = _rng.Next(10, 16);
            int attempts = 0;
            int added = 0;

            while (added < target && attempts < 50)
            {
                // 80% trong pool, 20% random ngoài pool
                var source = _rng.Next(100) < 80 ? pool : approvedPosts;
                var post = source[_rng.Next(source.Count)];

                if (favKeys.Add((tenant.Id, post.RentalPostId)))
                {
                    favorites.Add(new Favorite
                    {
                        UserId = tenant.Id,
                        RentalPostId = post.RentalPostId,
                        CreatedAt = DateTime.UtcNow.AddDays(-_rng.Next(1, 90))
                    });
                    added++;
                }
                attempts++;
            }
        }
        db.Favorites.AddRange(favorites);

        // ---------- RENTAL REQUESTS: 4-8 per tenant ----------
        var requestFaker = new Faker<RentalRequest>("vi")
            .RuleFor(r => r.Message, f => f.Lorem.Sentences(2))
            .RuleFor(r => r.Status, f => f.PickRandom<RentalRequestStatus>())
            .RuleFor(r => r.CreatedAt, f => f.Date.Past(3));

        var requests = new List<RentalRequest>();
        foreach (var tenant in tenants)
        {
            var pool = GetPreferredPosts(tenant);
            int target = _rng.Next(4, 9);

            for (int i = 0; i < target; i++)
            {
                var req = requestFaker.Generate();
                req.TenantId = tenant.Id;
                req.RentalPostId = pool[_rng.Next(pool.Count)].RentalPostId;
                requests.Add(req);
            }
        }
        db.RentalRequests.AddRange(requests);

        // ---------- VIEWINGS: 4-7 per tenant ----------
        var viewFaker = new Faker<ViewingAppointment>("vi")
            .RuleFor(v => v.ViewingDate, f => f.Date.Soon(15))
            .RuleFor(v => v.Note, f => f.Lorem.Sentence())
            .RuleFor(v => v.Status, f => f.PickRandom<ViewingAppointmentStatus>())
            .RuleFor(v => v.CreatedAt, f => f.Date.Past(1));

        var viewings = new List<ViewingAppointment>();
        foreach (var tenant in tenants)
        {
            var pool = GetPreferredPosts(tenant);
            int target = _rng.Next(4, 8);

            for (int i = 0; i < target; i++)
            {
                var v = viewFaker.Generate();
                v.TenantId = tenant.Id;
                v.RentalPostId = pool[_rng.Next(pool.Count)].RentalPostId;
                if (v.Status is ViewingAppointmentStatus.Confirmed or ViewingAppointmentStatus.Completed)
                    v.LandlordNote = "Đã xác nhận, hẹn gặp đúng giờ.";
                viewings.Add(v);
            }
        }
        db.ViewingAppointments.AddRange(viewings);

        await db.SaveChangesAsync();

        // ---------- CONVERSATIONS: 3-6 per tenant ----------
        var conversations = new List<Conversation>();
        foreach (var tenant in tenants)
        {
            var pool = GetPreferredPosts(tenant);
            int target = _rng.Next(3, 7);

            for (int i = 0; i < target; i++)
            {
                var post = pool[_rng.Next(pool.Count)];
                conversations.Add(new Conversation
                {
                    InitiatorId = tenant.Id,
                    RecipientId = post.OwnerId,
                    RentalPostId = post.RentalPostId,
                    ConversationType = ConversationType.TenantLandlord,
                    CreatedAt = DateTime.UtcNow.AddDays(-_rng.Next(1, 60))
                });
            }
        }
        db.Conversations.AddRange(conversations);
        await db.SaveChangesAsync();

        // ---------- MESSAGES ----------
        var msgFaker = new Faker("vi");
        var messages = new List<Message>();
        foreach (var c in conversations)
        {
            int count = _rng.Next(5, 13);
            var t = c.CreatedAt;
            for (int i = 0; i < count; i++)
            {
                bool fromInitiator = i % 2 == 0;
                messages.Add(new Message
                {
                    ConversationId = c.ConversationId,
                    SenderId = fromInitiator ? c.InitiatorId : c.RecipientId,
                    Content = msgFaker.Lorem.Sentence(8),
                    IsRead = i < count - 1,
                    SentAt = t.AddMinutes(i * _rng.Next(5, 60))
                });
            }
            c.UpdatedAt = messages.Last().SentAt;
        }
        db.Messages.AddRange(messages);
        await db.SaveChangesAsync();

        // ⭐ ĐẾM INTERACTIONS THỰC TẾ CHO MODEL
        int totalInteractions = favorites.Count + requests.Count
                              + viewings.Count + conversations.Count;

        Console.WriteLine($"   → {favorites.Count} favorites, {requests.Count} requests, " +
                          $"{viewings.Count} viewings, {conversations.Count} conversations, " +
                          $"{messages.Count} messages");
        Console.WriteLine($"   → TOTAL INTERACTIONS (cho model): {totalInteractions}");
    }

    // ================================================================
    // 6. CONTRACTS + METER READINGS + INVOICES
    // ================================================================
    private static async Task SeedContractsAndInvoicesAsync(
        AppDbContext db, List<RentalPost> posts, List<ApplicationUser> tenants)
    {
        var approved = posts
            .Where(p => p.ApprovalStatus == ApprovalStatus.Approved)
            .ToList();
        if (approved.Count == 0) return;

        // Chọn ~30% bài đăng Approved để tạo hợp đồng Active (tránh chiếm hết pool)
        var rentedPosts = approved
            .OrderBy(_ => _rng.Next())
            .Take(approved.Count / 3)
            .ToList();

        var contracts = new List<RentalContract>();
        foreach (var post in rentedPosts)
        {
            var tenant = tenants[_rng.Next(tenants.Count)];
            var start = DateTime.UtcNow.AddMonths(-_rng.Next(1, 6));
            var end = start.AddMonths(_rng.Next(6, 18));

            contracts.Add(new RentalContract
            {
                RentalPostId = post.RentalPostId,
                TenantId = tenant.Id,
                LandlordId = post.OwnerId,
                StartDate = start,
                EndDate = end,
                MonthlyRent = post.Price,
                DepositAmount = post.Price,
                AgreedElectricityPrice = post.ElectricityUnitPrice,
                AgreedWaterPrice = post.WaterUnitPrice,
                Terms = "Bên thuê thanh toán đúng hạn, giữ gìn vệ sinh chung, " +
                                          "không tự ý sửa chữa kết cấu.",
                Status = ContractStatus.Active,
                TenantConfirmedAt = start.AddDays(-1),
                LandlordConfirmedAt = start.AddDays(-1),
                CreatedAt = start.AddDays(-3)
            });
        }
        db.RentalContracts.AddRange(contracts);
        await db.SaveChangesAsync();

        var rentedPostIds = contracts.Select(c => c.RentalPostId).ToList();
        await db.RentalPosts
            .Where(p => rentedPostIds.Contains(p.RentalPostId))
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.RoomStatus, RoomStatus.Rented));

        foreach (var p in posts.Where(x => rentedPostIds.Contains(x.RentalPostId)))
            p.RoomStatus = RoomStatus.Rented;

        // ---------- Meter Readings (3 tháng gần nhất) ----------
        var readings = new List<MeterReading>();
        foreach (var c in contracts)
        {
            double prevE = _rng.Next(100, 3000);
            double prevW = _rng.Next(50, 500);

            for (int m = 3; m >= 1; m--)
            {
                var period = DateTime.UtcNow.AddMonths(-m);
                double eUsage = _rng.Next(80, 300);
                double wUsage = _rng.Next(5, 25);

                readings.Add(new MeterReading
                {
                    RentalPostId = c.RentalPostId,
                    Period = period.ToString("yyyy-MM"),
                    ReadingDate = period.AddMonths(1).AddDays(-_rng.Next(0, 3)),
                    PrevElectricIndex = prevE,
                    CurrElectricIndex = prevE + eUsage,
                    ElectricUsage = eUsage,
                    PrevWaterIndex = prevW,
                    CurrWaterIndex = prevW + wUsage,
                    WaterUsage = wUsage,
                    RecordedById = c.LandlordId,
                    Note = "Ghi chỉ số định kỳ"
                });

                prevE += eUsage;
                prevW += wUsage;
            }
        }
        db.MeterReadings.AddRange(readings);
        await db.SaveChangesAsync();

        // ---------- Invoices ----------
        var invoices = new List<Invoice>();
        foreach (var c in contracts)
        {
            var cReadings = readings.Where(r => r.RentalPostId == c.RentalPostId).ToList();
            foreach (var r in cReadings)
            {
                decimal eAmount = (decimal)r.ElectricUsage * c.AgreedElectricityPrice;
                decimal wAmount = (decimal)r.WaterUsage * c.AgreedWaterPrice;
                decimal svcFee = 200_000m;
                decimal total = c.MonthlyRent + eAmount + wAmount + svcFee;

                bool isPaid = _rng.Next(100) < 80;

                var periodDate = DateTime.ParseExact(r.Period, "yyyy-MM", null);
                var due = periodDate.AddMonths(1).AddDays(4);

                invoices.Add(new Invoice
                {
                    RentalContractId = c.RentalContractId,
                    MeterReadingId = r.MeterReadingId,
                    BillingPeriod = r.Period,
                    RoomRentAmount = c.MonthlyRent,
                    ElectricUsage = r.ElectricUsage,
                    ElectricAmount = eAmount,
                    WaterUsage = r.WaterUsage,
                    WaterAmount = wAmount,
                    ServiceFeeAmount = svcFee,
                    AdjustmentAmount = 0,
                    TotalAmount = total,
                    IssueDate = r.ReadingDate,
                    DueDate = due,
                    PaymentStatus = isPaid ? PaymentStatus.Paid : PaymentStatus.Unpaid,
                    PaidAt = isPaid ? r.ReadingDate.AddDays(_rng.Next(1, 10)) : null,
                    TenantConfirmedAt = isPaid ? r.ReadingDate.AddDays(1) : null,
                    VietQRContent = $"EZRENT {c.RentalContractId} {r.Period}",
                    CreatedAt = r.ReadingDate
                });
            }
        }
        db.Invoices.AddRange(invoices);
        await db.SaveChangesAsync();

        Console.WriteLine($"   → {contracts.Count} hợp đồng, {readings.Count} chỉ số, {invoices.Count} hóa đơn");
    }

    // ================================================================
    // 7. REPAIR REQUESTS
    // ================================================================
    private static async Task SeedRepairsAsync(
        AppDbContext db, List<ApplicationUser> tenants)
    {
        var rentedPostIds = await db.RentalPosts
            .Where(p => p.RoomStatus == RoomStatus.Rented)
            .Select(p => p.RentalPostId)
            .ToListAsync();

        if (rentedPostIds.Count == 0)
        {
            Console.WriteLine("   → 0 repair requests (không có phòng Rented)");
            return;
        }

        var faker = new Faker<RepairRequest>("vi")
            .RuleFor(r => r.ProblemType, f => f.PickRandom(ProblemTypes))
            .RuleFor(r => r.Description, f => f.Lorem.Sentences(2))
            .RuleFor(r => r.Priority, f => f.PickRandom<RepairPriority>())
            .RuleFor(r => r.Status, f => f.PickRandom<RepairRequestStatus>())
            .RuleFor(r => r.ImageUrl, f => f.Random.Bool(0.5f) ? f.Image.PicsumUrl() : null)
            .RuleFor(r => r.CreatedAt, f => f.Date.Past(4))
            .RuleFor(r => r.ResolutionNote, (f, r) =>
                r.Status == RepairRequestStatus.Resolved ? "Đã sửa xong và kiểm tra ổn định." : null)
            .RuleFor(r => r.ResolvedAt, (f, r) =>
                r.Status == RepairRequestStatus.Resolved
                    ? DateTime.UtcNow.AddDays(-f.Random.Int(1, 10))
                    : null);

        var repairs = new List<RepairRequest>();
        for (int i = 0; i < 25; i++)
        {
            var rp = faker.Generate();
            rp.RentalPostId = rentedPostIds[_rng.Next(rentedPostIds.Count)];
            rp.TenantId = tenants[_rng.Next(tenants.Count)].Id;
            repairs.Add(rp);
        }

        db.RepairRequests.AddRange(repairs);
        await db.SaveChangesAsync();

        Console.WriteLine($"   → {repairs.Count} repair requests");
    }

    // ================================================================
    // 8. NOTIFICATIONS
    // ================================================================
    private static async Task SeedNotificationsAsync(
        AppDbContext db,
        List<ApplicationUser> tenants,
        List<ApplicationUser> landlords,
        ApplicationUser admin)
    {
        var templates = new[]
        {
            new { Title = "Yêu cầu thuê mới",            Type = "RentalRequest" },
            new { Title = "Lịch xem phòng đã xác nhận",  Type = "ViewingAppointment" },
            new { Title = "Hóa đơn mới",                 Type = "Invoice" },
            new { Title = "Hợp đồng đã kích hoạt",       Type = "Contract" },
            new { Title = "Yêu cầu sửa chữa",            Type = "RepairRequest" },
            new { Title = "Tin đăng đã được duyệt",      Type = "Approval" },
            new { Title = "Tin nhắn mới",                Type = "Message" }
        };

        var notifications = new List<Notification>();

        foreach (var user in tenants.Concat(landlords).Append(admin))
        {
            int count = _rng.Next(2, 6);
            for (int i = 0; i < count; i++)
            {
                var t = templates[_rng.Next(templates.Length)];
                notifications.Add(new Notification
                {
                    UserId = user.Id,
                    Title = t.Title,
                    Content = $"Bạn có thông báo mới: {t.Title.ToLower()}.",
                    Link = "/",
                    Type = t.Type,
                    IsRead = _rng.Next(100) < 50,
                    CreatedAt = DateTime.UtcNow.AddDays(-_rng.Next(1, 30))
                });
            }
        }

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync();

        Console.WriteLine($"   → {notifications.Count} notifications");
    }
}