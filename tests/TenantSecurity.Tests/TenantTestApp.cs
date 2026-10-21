using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using EZ_Rent.Areas.Tenant.Controllers;
using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Models.Enums;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using Xunit;
using EZ_Rent.Services;
using Microsoft.AspNetCore.SignalR;

namespace TenantSecurity.Tests;

// An independent MVC host: never executes the application's Program.cs or uses its SQL Server.
public sealed class TenantTestApp : IAsyncLifetime
{
    public const string TenantA = "tenant-a-private";
    public const string TenantB = "tenant-b-private";
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    private WebApplication app = null!;
    public HttpClient Client { get; private set; } = null!;
    public ITenantViewingScheduleReader? ViewingScheduleReader { get; set; }
    public TimeProvider? ViewingClock { get; set; }
    public bool UseIdentityCookies { get; set; }
    public string PrivateFileRoot { get; } = Path.Combine(Path.GetTempPath(), "ez-rent-test-files-" + Guid.NewGuid().ToString("N"));

    public Task InitializeAsync() => StartAsync(false);

    public Task StartPreviewAsync() => StartAsync(true);
    public Task WaitForPreviewShutdownAsync() => app.WaitForShutdownAsync();

    private async Task StartAsync(bool preview)
    {
        await connection.OpenAsync();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(FavoriteController).Assembly.GetName().Name,
            EnvironmentName = "Testing",
            ContentRootPath = FindProjectRoot()
        });
        if (!preview) builder.WebHost.UseTestServer();
        else builder.WebHost.UseUrls("http://127.0.0.1:" + (Environment.GetEnvironmentVariable("EZRENT_PREVIEW_PORT") ?? "8770"));
        builder.Logging.ClearProviders();
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(FavoriteController).Assembly);
        builder.Services.AddRazorPages();
        builder.Services.AddMemoryCache();
        builder.Services.AddScoped<RecommendationService>();
        builder.Services.AddSignalR();
        builder.Services.AddScoped<ChatRealtime>();
        builder.Services.Configure<RentalFileStorageOptions>(options => options.RootPath = PrivateFileRoot);
        builder.Services.AddSingleton<RentalPrivateFileStore>();
        builder.Services.AddScoped<RentalDocumentService>();
        builder.Services.AddScoped<TenantContractDraftService>();
        if (ViewingClock != null) builder.Services.AddSingleton<TimeProvider>(ViewingClock);
        if (preview) builder.Services.AddSingleton<ITenantViewingScheduleReader, PreviewViewingScheduleReader>();
        else if (ViewingScheduleReader != null) builder.Services.AddSingleton<ITenantViewingScheduleReader>(ViewingScheduleReader);
        builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlite(connection));
        builder.Services.AddIdentity<ApplicationUser, IdentityRole>()
            .AddEntityFrameworkStores<AppDbContext>().AddDefaultTokenProviders();
        // AddIdentity in this fixture has no default UI registration; mirror its production routes.
        builder.Services.ConfigureApplicationCookie(o =>
        {
            o.LoginPath = "/Identity/Account/Login";
            o.AccessDeniedPath = "/Identity/Account/AccessDenied";
        });
        builder.Services.AddAuthentication(o =>
        {
            o.DefaultAuthenticateScheme = preview || UseIdentityCookies ? IdentityConstants.ApplicationScheme : "Test";
            o.DefaultChallengeScheme = preview || UseIdentityCookies ? IdentityConstants.ApplicationScheme : "Test";
            o.DefaultForbidScheme = preview || UseIdentityCookies ? IdentityConstants.ApplicationScheme : "Test";
        }).AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>("Test", _ => { });
        app = builder.Build();
        if (preview)
        {
            // Only this test executable supplies a demo identity. Production registration is unchanged.
            app.Use(async (context, next) =>
            {
                // Sign in once for review. A marker prevents silently signing back in after logout.
                if (!context.Request.Cookies.ContainsKey("tenant-preview-entered"))
                {
                    var manager = context.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
                    var demo = await manager.FindByIdAsync(TenantA);
                    if (demo != null)
                    {
                        await context.RequestServices.GetRequiredService<SignInManager<ApplicationUser>>()
                            .SignInAsync(demo, isPersistent: false);
                        context.Response.Cookies.Append("tenant-preview-entered", "1", new CookieOptions { HttpOnly = true, SameSite = SameSiteMode.Lax });
                    }
                }
                // Same-origin review frame: anti-forgery SAMEORIGIN headers remain intact.
                if (HttpMethods.IsGet(context.Request.Method) && context.Request.Path == "/review/tenant")
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    context.Response.Headers.CacheControl = "no-store";
                    await context.Response.SendFileAsync(Path.Combine(app.Environment.ContentRootPath,
                        "docs", "tenant-app-responsive-review.html"));
                    return;
                }
                // Design prototype files are available only in this isolated preview host.
                var documentPreview = context.Request.Path.Value switch
                {
                    "/review/tenant-documents" => ("tenant-documents-preview.html", "text/html; charset=utf-8"),
                    "/review/tenant-documents.css" => ("tenant-documents-preview.css", "text/css; charset=utf-8"),
                    "/review/tenant-documents.js" => ("tenant-documents-preview.js", "text/javascript; charset=utf-8"),
                    _ => default
                };
                if (HttpMethods.IsGet(context.Request.Method) && documentPreview.Item1 != null)
                {
                    context.Response.ContentType = documentPreview.Item2;
                    context.Response.Headers.CacheControl = "no-store";
                    await context.Response.SendFileAsync(Path.Combine(app.Environment.ContentRootPath,
                        "docs", documentPreview.Item1));
                    return;
                }
                if (context.Request.Path == "/") { context.Response.Redirect("/Tenant/Dashboard"); return; }
                var allowed = context.Request.Path.StartsWithSegments("/Identity/Account/Logout") || context.Request.Path.StartsWithSegments("/hubs/chat") || context.Request.Path.StartsWithSegments("/Landlord/Messages") || context.Request.Path.StartsWithSegments("/Tenant/Support") || context.Request.Path.StartsWithSegments("/Tenant/MyRoom") || context.Request.Path.StartsWithSegments("/Tenant/Contract") || context.Request.Path.StartsWithSegments("/Tenant/Invoice") || context.Request.Path.StartsWithSegments("/Tenant/RepairRequest") || context.Request.Path.StartsWithSegments("/Tenant/Account") || context.Request.Path.StartsWithSegments("/Identity/Account/Login") || context.Request.Path.StartsWithSegments("/uploads/avatars") || context.Request.Path.StartsWithSegments("/lib") || context.Request.Path.StartsWithSegments("/Tenant/Dashboard") || context.Request.Path.StartsWithSegments("/Tenant/Notification") || context.Request.Path.StartsWithSegments("/Tenant/RentalPost") || context.Request.Path.StartsWithSegments("/Tenant/Favorite") || context.Request.Path.StartsWithSegments("/Tenant/ViewingAppointment") || context.Request.Path.StartsWithSegments("/Tenant/ViewingBooking") ||
                    context.Request.Path.StartsWithSegments("/css") || context.Request.Path.StartsWithSegments("/js") || context.Request.Path.StartsWithSegments("/images") || context.Request.Path.StartsWithSegments("/fonts");
                if (!allowed)
                {
                    context.Response.ContentType = "text/html; charset=utf-8";
                    await context.Response.WriteAsync("<!doctype html><html lang=\"vi\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width\"><title>Preview gói 3A</title><p>Mục này thuộc gói chức năng khác.</p><a href=\"/Tenant/RentalPost\">Quay lại tìm phòng</a></html>");
                    return;
                }
                if (context.Request.Path.StartsWithSegments("/hubs/chat")) { await next(); return; }
                var original = context.Response.Body;
                await using var buffer = new MemoryStream();
                context.Response.Body = buffer;
                try
                {
                    await next();
                    buffer.Position = 0;
                    if (context.Response.ContentType?.StartsWith("text/html") == true && context.Request.Headers["X-Requested-With"] != "XMLHttpRequest")
                    {
                        using var reader = new StreamReader(buffer, leaveOpen: true);
                        var html = await reader.ReadToEndAsync();
                        html = Regex.Replace(html, "<body[^>]*>", body => body.Value + "<div class=\"tenant-preview-bar\" style=\"padding:8px 12px;background:#fff5de;color:#78591e;text-align:center;font:12px Segoe UI,Arial\">Bản chạy thử Tenant · Dữ liệu minh họa · Không gửi đến chủ nhà thật</div>");
                        context.Response.ContentLength = null;
                        context.Response.Body = original;
                        await context.Response.WriteAsync(html);
                    }
                    else await buffer.CopyToAsync(original);
                }
                finally { context.Response.Body = original; }
            });
            app.UseStaticFiles();
        }
        if (preview) app.UseWebSockets();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllerRoute("areas", "{area:exists}/{controller=Home}/{action=Index}/{id?}");
        app.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}");
        app.MapRazorPages();
        app.MapHub<ChatHub>("/hubs/chat", o => o.CloseOnAuthenticationExpiration = true);
        await app.StartAsync();
        Client = preview ? new HttpClient { BaseAddress = new Uri("http://127.0.0.1:" + (Environment.GetEnvironmentVariable("EZRENT_PREVIEW_PORT") ?? "8770")) } : app.GetTestClient();
        Client.Timeout = TimeSpan.FromSeconds(10);
        Client.DefaultRequestHeaders.Add("X-Test-User", TenantA);
        Client.DefaultRequestHeaders.Add("X-Test-Role", "Tenant");
        await WithDb(async db =>
        {
            await db.Database.EnsureCreatedAsync();
            db.Users.AddRange(new ApplicationUser { Id = TenantA, FullName = "Tenant A", UserName = TenantA },
                new ApplicationUser { Id = TenantB, FullName = "Tenant B", UserName = TenantB },
                new ApplicationUser { Id = "landlord", FullName = "Landlord", UserName = "landlord" });
            for (var id = 1; id <= 6; id++)
                db.RentalPosts.Add(new RentalPost
                {
                    RentalPostId = id, Title = $"Room {id}", Description = "Test room", Address = $"Address {id}",
                    City = "Test city", OwnerId = "landlord", RoomType = RoomType.Room,
                    RoomStatus = id is 1 or 2 or 6 ? RoomStatus.Rented : id == 5 ? RoomStatus.Hidden : RoomStatus.Available,
                    ApprovalStatus = id == 4 ? ApprovalStatus.Draft : ApprovalStatus.Approved
                });
            for (var id = 1; id <= 2; id++)
            {
                var tenantId = id == 1 ? TenantA : TenantB;
                db.Favorites.Add(new Favorite { FavoriteId = id, UserId = tenantId, RentalPostId = 3 });
                db.ViewingAppointments.Add(new ViewingAppointment
                {
                    ViewingAppointmentId = id, TenantId = tenantId, RentalPostId = 3,
                    ViewingDate = DateTime.UtcNow.AddDays(3), LandlordNote = "Private landlord note"
                });
                db.RentalContracts.Add(new RentalContract
                {
                    RentalContractId = id, TenantId = tenantId, LandlordId = "landlord", RentalPostId = id,
                    StartDate = DateTime.Today.AddMonths(-1), EndDate = DateTime.Today.AddMonths(6),
                    Status = ContractStatus.Active, MonthlyRent = 3000000
                });
                db.Invoices.Add(new Invoice
                {
                    InvoiceId = id, RentalContractId = id, BillingPeriod = "2026-10", TotalAmount = 3500000,
                    DueDate = DateTime.UtcNow.AddDays(7), Note = tenantId
                });
                db.RepairRequests.Add(new RepairRequest
                {
                    RepairRequestId = id, TenantId = tenantId, RentalPostId = id,
                    ProblemType = "Electricity", Description = "Broken light", ResolutionNote = "Landlord only"
                });
            }
            await db.SaveChangesAsync();
        });
        if (preview)
        {
            await SeedPreviewAsync();
            await SeedRentalPreviewAsync();
        }
        if (preview || UseIdentityCookies) await SeedAccountAsync();
        if (preview) await SeedSupportPreviewAsync();
        if (preview) await SeedRentalDocumentsPreviewAsync();
    }

    private static string FindProjectRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "EZ-Rent.csproj"))) return directory.FullName;
        throw new DirectoryNotFoundException("Không tìm thấy thư mục dự án EZ-Rent.");
    }

    private async Task SeedPreviewAsync() => await WithDb(async db =>
    {
        (await db.Users.SingleAsync(u => u.Id == TenantA)).FullName = "Minh Anh";
        (await db.Users.SingleAsync(u => u.Id == "landlord")).FullName = "Anh Tuấn";
        var existing = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 3);
        existing.Title = "Phòng có ban công, đón nắng mỗi sáng"; existing.City = "Hà Nội"; existing.District = "Cầu Giấy";
        existing.Address = "Ngõ 68, phố Cầu Giấy"; existing.Price = 3500000; existing.Area = 28;
        existing.Description = "Không gian sáng và thoáng, phù hợp cho 1–2 người.\nCó khu bếp riêng, cửa sổ lớn và chỗ để xe ở tầng một.";
        existing.AmenitiesJson = "[\"Điều hòa\",\"Nóng lạnh\",\"Chỗ để xe\",\"Bếp riêng\"]";
        existing.ElectricityUnitPrice = 3800; existing.WaterUnitPrice = 35000;
        db.RoomImages.Add(new RoomImage { RentalPostId = 3, MediaUrl = "/images/tenant-preview/living.jpg", IsPrimary = true });
        db.RoomImages.Add(new RoomImage { RentalPostId = 3, MediaUrl = "/images/room-placeholder.svg?view=2" });
        db.Services.Add(new Service { RentalPostId = 3, Name = "Internet", UnitPrice = 100000, Unit = "tháng" });
        var titles = new[] { "Căn hộ nhỏ, đầy đủ tiện nghi", "Phòng sáng gần trường đại học", "Không gian yên tĩnh, có gác lửng", "Phòng thoáng, cửa sổ rộng", "Căn hộ có bếp và ban công", "Phòng mới trong khu dân cư" };
        for (var i = 0; i < 17; i++)
        {
            var post = new RentalPost
            {
                RentalPostId = 7 + i, Title = titles[i % titles.Length], Description = "Phòng được bố trí gọn gàng, thuận tiện đi lại.\nHãy xác nhận thông tin và các khoản phí với chủ nhà trước khi thuê.",
                Price = 1800000 + i * 200000, Area = 18 + i * 2, Address = $"Ngõ {20 + i}, khu dân cư",
                City = i % 3 == 0 ? "Hồ Chí Minh" : "Hà Nội", District = i % 3 == 0 ? "Bình Thạnh" : i % 2 == 0 ? "Cầu Giấy" : "Thanh Xuân",
                RoomType = i % 3 == 0 ? RoomType.Apartment : RoomType.Room, OwnerId = "landlord",
                ApprovalStatus = ApprovalStatus.Approved, AmenitiesJson = "[\"Wifi\",\"Chỗ để xe\"]", ElectricityUnitPrice = 3800, WaterUnitPrice = 35000,
                CreatedAt = DateTime.UtcNow.AddDays(-i - 1)
            };
            post.RoomImages.Add(new RoomImage { MediaUrl = "/images/room-placeholder.svg" });
            db.RentalPosts.Add(post);
        }
        var viewing = await db.ViewingAppointments.SingleAsync(a => a.ViewingAppointmentId == 1);
        var demoDay = DateTime.Today.AddDays(2);
        viewing.ViewingDate = demoDay.AddHours(14); viewing.Note = "Mình muốn xem chỗ để xe và khu bếp.";
        viewing.LandlordNote = null;
        db.ViewingAppointments.AddRange(
            new ViewingAppointment { ViewingAppointmentId = 3, RentalPostId = 7, TenantId = TenantA, ViewingDate = demoDay.AddDays(1).AddHours(16), Status = ViewingAppointmentStatus.Confirmed, Note = "Mình đi cùng một người bạn.", LandlordNote = "Hẹn bạn tại cổng. Khi đến bạn nhắn mình nhé." },
            new ViewingAppointment { ViewingAppointmentId = 4, RentalPostId = 8, TenantId = TenantA, ViewingDate = demoDay.AddDays(-7).AddHours(10), Status = ViewingAppointmentStatus.Completed, LandlordNote = "Cảm ơn bạn đã đến xem phòng." },
            new ViewingAppointment { ViewingAppointmentId = 5, RentalPostId = 9, TenantId = TenantA, ViewingDate = demoDay.AddDays(-3).AddHours(15), Status = ViewingAppointmentStatus.Cancelled, Note = "Mình thay đổi kế hoạch tìm phòng." },
            new ViewingAppointment { ViewingAppointmentId = 6, RentalPostId = 3, TenantId = TenantA, ViewingDate = demoDay.AddDays(-2).AddHours(9), Status = ViewingAppointmentStatus.Rejected, LandlordNote = "Mình bận vào giờ bạn đề xuất. Cảm ơn bạn đã quan tâm." });
        db.Favorites.Add(new Favorite { UserId = TenantA, RentalPostId = 7 });
        db.Notifications.AddRange(
            new Notification { UserId = TenantA, Title = "Chủ nhà đã xác nhận lịch xem phòng", Content = "Lịch đã được xác nhận. Bạn có thể mở chi tiết để xem ngày, giờ và địa chỉ.", Link = "/Tenant/ViewingAppointment/Details/3", CreatedAt = DateTime.UtcNow.AddMinutes(-20) },
            new Notification { UserId = TenantA, Title = "Yêu cầu của bạn đang chờ phản hồi", Content = "Theo dõi trạng thái yêu cầu xem phòng. Hãy đợi chủ nhà xác nhận trước khi đến.", Link = "/Tenant/ViewingAppointment/Details/1", CreatedAt = DateTime.UtcNow.AddHours(-1) },
            new Notification { UserId = TenantA, Title = "Chào mừng bạn đến với EZ Rent", Content = "Tìm phòng, lưu lựa chọn và theo dõi lịch hẹn ngay tại đây.", IsRead = true, CreatedAt = DateTime.UtcNow.AddDays(-1) });
        db.Users.AddRange(new ApplicationUser { Id = "preview-ha", FullName = "Chị Hà", UserName = "preview-ha" },
            new ApplicationUser { Id = "preview-minh", FullName = "Anh Minh", UserName = "preview-minh" });
        db.RentalPosts.Local.Single(p => p.RentalPostId == 7).OwnerId = "preview-ha";
        db.RentalPosts.Local.Single(p => p.RentalPostId == 8).OwnerId = "preview-minh";
        var date = DateTime.UtcNow.Date;
        db.Conversations.AddRange(
            new Conversation { ConversationId = 101, InitiatorId = TenantA, RecipientId = "landlord", RentalPostId = 3, ConversationType = ConversationType.TenantLandlord,
                Messages = new List<Message> {
                    new() { SenderId = "landlord", Content = "Chào Minh Anh, phòng vẫn còn nhé. Bạn muốn hỏi thêm thông tin gì?", SentAt = date.AddHours(2) },
                    new() { SenderId = TenantA, Content = "Em chào anh. Em muốn hỏi thêm về các khoản phí và chỗ để xe ạ.", SentAt = date.AddHours(2).AddMinutes(5), IsRead = true },
                    new() { SenderId = "landlord", Content = "Điện 3.800 đ/kWh, nước 35.000 đ/m³. Gửi xe 100.000 đ/tháng. Bạn cứ nhắn mình nếu cần làm rõ nhé.", SentAt = date.AddHours(4) } } },
            new Conversation { ConversationId = 102, InitiatorId = "preview-ha", RecipientId = TenantA, RentalPostId = 7, ConversationType = ConversationType.TenantLandlord,
                Messages = new List<Message> {
                    new() { SenderId = TenantA, Content = "Em chào chị, phòng này có thể ở hai người được không ạ?", SentAt = date.AddHours(3), IsRead = true },
                    new() { SenderId = "preview-ha", Content = "Được em nhé. Chị sẽ đón em theo lịch đã xác nhận. Khi đến cổng em nhắn chị nhé.", SentAt = date.AddHours(3).AddMinutes(5) } } },
            new Conversation { ConversationId = 103, InitiatorId = TenantA, RecipientId = "preview-minh", RentalPostId = 8, ConversationType = ConversationType.TenantLandlord,
                Messages = new List<Message> {
                    new() { SenderId = TenantA, Content = "Em muốn hỏi thêm về khu bếp và chỗ để xe ạ.", SentAt = date.AddDays(-1).AddHours(3), IsRead = true },
                    new() { SenderId = "preview-minh", Content = "Phòng có khu bếp riêng và chỗ để xe ở tầng một nhé.", SentAt = date.AddDays(-1).AddHours(4) } } });
        await db.SaveChangesAsync();
    });

    private async Task SeedRentalPreviewAsync() => await WithDb(async db =>
    {
        var room = await db.RentalPosts.SingleAsync(p => p.RentalPostId == 1);
        room.Title = "Phòng ban công tại Cầu Giấy";
        room.Address = "Ngõ 68, phố Cầu Giấy"; room.District = "Cầu Giấy"; room.City = "Hà Nội";
        room.Area = 28; room.Price = 3500000;
        db.RoomImages.Add(new RoomImage { RentalPostId = 1, MediaUrl = "/images/tenant-preview/living.jpg", IsPrimary = true });
        (await db.Users.SingleAsync(u => u.Id == "landlord")).PhoneNumber = "0902345678";
        var contract = await db.RentalContracts.SingleAsync(c => c.RentalContractId == 1);
        var month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        contract.StartDate = month.AddYears(-1); contract.EndDate = month.AddMonths(6).AddDays(-1);
        contract.MonthlyRent = contract.DepositAmount = 3500000;
        contract.AgreedElectricityPrice = 3800; contract.AgreedWaterPrice = 35000;
        contract.Terms = "Thanh toán tiền thuê theo kỳ ghi trong hóa đơn.\nGiữ gìn tài sản chung và thông báo khi có sự cố.\nHai bên trao đổi trước khi thay đổi thời hạn thuê.";
        var reading = new MeterReading
        {
            RentalPostId = 1, Period = month.ToString("yyyy-MM"), RecordedById = "landlord",
            ReadingDate = month.AddDays(1), PrevElectricIndex = 1050, CurrElectricIndex = 1095, ElectricUsage = 45,
            PrevWaterIndex = 100, CurrWaterIndex = 102, WaterUsage = 2
        };
        db.MeterReadings.Add(reading);
        var invoice = await db.Invoices.SingleAsync(i => i.InvoiceId == 1);
        invoice.BillingPeriod = month.ToString("yyyy-MM"); invoice.IssueDate = month.AddDays(1); invoice.DueDate = month.AddDays(24);
        invoice.RoomRentAmount = 3500000; invoice.ElectricUsage = 45; invoice.ElectricAmount = 171000;
        invoice.WaterUsage = 2; invoice.WaterAmount = 70000; invoice.ServiceFeeAmount = 100000;
        invoice.AdjustmentAmount = -41000; invoice.TotalAmount = 3800000; invoice.MeterReading = reading;
        invoice.Note = "Điều chỉnh giảm 41.000 đ theo thỏa thuận trong kỳ.";
        for (var i = 1; i <= 11; i++)
        {
            var billingMonth = month.AddMonths(-i);
            var status = i == 1 ? PaymentStatus.AwaitingConfirmation : i == 2 ? PaymentStatus.Overdue : PaymentStatus.Paid;
            db.Invoices.Add(new Invoice
            {
                RentalContractId = 1, BillingPeriod = billingMonth.ToString("yyyy-MM"), IssueDate = billingMonth.AddDays(1),
                DueDate = billingMonth.AddDays(24), RoomRentAmount = 3500000, ServiceFeeAmount = 100000, TotalAmount = 3600000,
                PaymentStatus = status, PaidAt = status == PaymentStatus.Paid ? billingMonth.AddDays(10) : null
            });
        }
        db.RentalPosts.Add(new RentalPost
        {
            RentalPostId = 24, OwnerId = "preview-ha", Title = "Căn hộ nhỏ gần công viên", Address = "Phố Duy Tân",
            City = "Hà Nội", District = "Cầu Giấy", Description = "Phòng thuê minh họa thứ hai.", Price = 4200000,
            RoomStatus = RoomStatus.Rented, ApprovalStatus = ApprovalStatus.Approved, RoomType = RoomType.Apartment
        });
        db.RentalContracts.Add(new RentalContract
        {
            RentalContractId = 3, RentalPostId = 24, TenantId = TenantA, LandlordId = "preview-ha",
            StartDate = contract.StartDate.AddMonths(-1), EndDate = month.AddMonths(3),
            MonthlyRent = 4200000, DepositAmount = 4200000, Status = ContractStatus.Active
        });
        db.Invoices.Add(new Invoice
        {
            RentalContractId = 3, BillingPeriod = month.ToString("yyyy-MM"), RoomRentAmount = 4200000, TotalAmount = 4200000,
            DueDate = month.AddDays(24), PaymentStatus = PaymentStatus.Unpaid
        });
        var request = await db.RepairRequests.SingleAsync(r => r.RepairRequestId == 1);
        request.ProblemType = "Điện"; request.Description = "Đèn trần nhấp nháy khi bật, bắt đầu từ tối qua.";
        request.ResolutionNote = null; request.CreatedAt = DateTime.UtcNow.AddHours(-2);
        var topics = new[] { "Nước", "Khóa cửa", "Thiết bị trong phòng", "Điện" };
        for (var i = 0; i < 12; i++)
        {
            var status = (RepairRequestStatus)(i % 4 + 1);
            db.RepairRequests.Add(new RepairRequest
            {
                RentalPostId = 1, TenantId = TenantA, ProblemType = topics[i % 4],
                Description = i % 2 == 0 ? "Mình muốn nhờ chủ nhà kiểm tra thiết bị hoạt động chưa ổn định." : "Thiết bị có dấu hiệu cần kiểm tra, nhờ chủ nhà hỗ trợ.",
                Status = status, CreatedAt = DateTime.UtcNow.AddDays(-i - 1),
                ResolutionNote = status == RepairRequestStatus.Resolved ? "Đã kiểm tra và thay bộ phận hỏng." : null,
                ResolvedAt = status == RepairRequestStatus.Resolved ? DateTime.UtcNow.AddDays(-i) : null
            });
        }
        await db.SaveChangesAsync();
    });

    private async Task SeedRentalDocumentsPreviewAsync() => await WithServices(async services =>
    {
        var documents = services.GetRequiredService<RentalDocumentService>();
        var root = FindProjectRoot();
        var bytes = await File.ReadAllBytesAsync(Path.Combine(root, "docs", "templates", "bm03-hop-dong-thue-phong.pdf"));
        for (var version = 0; version < 2; version++)
        {
            using var source = new MemoryStream(bytes);
            var file = new FormFile(source, 0, bytes.Length, "document", $"BM03-minh-hoa-chua-ky-v{version + 1}.pdf");
            await documents.AppendContractAsync(1, "landlord", version, file);
        }
        var fixture = Path.Combine(root, "tests", "TenantSecurity.Tests", "Fixtures");
        var electricBytes = await File.ReadAllBytesAsync(Path.Combine(fixture, "electric-meter.png"));
        var waterBytes = await File.ReadAllBytesAsync(Path.Combine(fixture, "water-meter.png"));
        using var electric = new MemoryStream(electricBytes);
        using var water = new MemoryStream(waterBytes);
        await documents.CaptureInvoiceAsync(1, "landlord",
            new FormFile(electric, 0, electricBytes.Length, "electric", "electric.png"),
            new FormFile(water, 0, waterBytes.Length, "water", "water.png"));
    });

    private async Task SeedSupportPreviewAsync()
    {
        await WithServices(async services => {
            var users = services.GetRequiredService<UserManager<ApplicationUser>>();
            var other = (await users.FindByIdAsync(TenantB))!;
            other.UserName = other.Email = "tenantb@example.test"; other.EmailConfirmed = true;
            Assert.True((await users.UpdateAsync(other)).Succeeded);
            Assert.True((await users.AddPasswordAsync(other, "TenantDemo@123")).Succeeded);
            Assert.True((await users.AddToRoleAsync(other, "Tenant")).Succeeded);
        });
        await WithDb(async db =>
    {
        db.Users.Add(new ApplicationUser { Id = "preview-admin", UserName = "support@example.test", FullName = "Hỗ trợ EZ Rent" });
        db.Roles.Add(new IdentityRole { Id = "preview-admin-role", Name = "Admin", NormalizedName = "ADMIN" });
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = "preview-admin", RoleId = "preview-admin-role" });
        db.Conversations.Add(new Conversation { ConversationId = 104, InitiatorId = TenantA, RecipientId = "preview-admin",
            ConversationType = ConversationType.TenantAdmin, Messages = new List<Message> {
                new() { SenderId = TenantA, Content = "Cần làm rõ thông tin phí dịch vụ\nVấn đề: Thông tin phòng không đúng\n\nTôi muốn biết khoản phí nào được ghi trong tin phòng.", SentAt = DateTime.UtcNow.AddHours(-3), IsRead = true },
                new() { SenderId = "preview-admin", Content = "Đây là phản hồi minh họa để review giao diện. Bạn có thể bổ sung phần thông tin cần đối chiếu tại đây.", SentAt = DateTime.UtcNow.AddHours(-2) } } });
        await db.SaveChangesAsync();
    });

    }

    // Real Identity credentials only in this isolated fixture, never in the application database.
    private async Task SeedAccountAsync() => await WithServices(async services =>
    {
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        await roles.CreateAsync(new IdentityRole("Tenant"));
        if (app.Environment.IsEnvironment("Testing")) await roles.CreateAsync(new IdentityRole("Landlord"));
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        var user = (await users.FindByIdAsync(TenantA))!;
        user.UserName = user.Email = "minhanh@example.test";
        user.EmailConfirmed = true;
        user.PhoneNumber = "0901234567";
        Assert.True((await users.UpdateAsync(user)).Succeeded);
        Assert.True((await users.AddPasswordAsync(user, "TenantDemo@123")).Succeeded);
        Assert.True((await users.AddToRoleAsync(user, "Tenant")).Succeeded);
        var landlord = (await users.FindByIdAsync("landlord"))!;
        landlord.UserName = landlord.Email = "chutro@example.test"; landlord.EmailConfirmed = true;
        Assert.True((await users.UpdateAsync(landlord)).Succeeded);
        Assert.True((await users.AddPasswordAsync(landlord, "LandlordDemo@123")).Succeeded);
        Assert.True((await users.AddToRoleAsync(landlord, "Landlord")).Succeeded);
    });

    public async Task WithServices(Func<IServiceProvider, Task> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await action(scope.ServiceProvider);
    }

    public async Task WithDb(Func<AppDbContext, Task> action)
    {
        await using var scope = app.Services.CreateAsyncScope();
        await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public async Task<HttpResponseMessage> PostWithToken(string url, Dictionary<string, string> fields,
        string tokenPage = "/Tenant/Favorite/Create")
    {
        using var form = await Client.GetAsync(tokenPage);
        form.EnsureSuccessStatusCode();
        var html = await form.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, "Razor must emit an anti-forgery token.");
        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new FormUrlEncodedContent(fields)
        };
        request.Headers.Add("Cookie", string.Join("; ", form.Headers.GetValues("Set-Cookie").Select(v => v.Split(';')[0])));
        return await Client.SendAsync(request);
    }

    public async Task DisposeAsync()
    {
        Client.Dispose();
        await app.DisposeAsync();
        await connection.DisposeAsync();
        // Only delete flat files inside this fixture's own generated temporary directory.
        var expectedParent = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var resolved = Path.GetFullPath(PrivateFileRoot);
        if (resolved.StartsWith(expectedParent + "ez-rent-test-files-", StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
        {
            foreach (var file in Directory.EnumerateFiles(resolved)) File.Delete(file);
            Directory.Delete(resolved);
        }
    }
}

public sealed class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public HeaderAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder) : base(options, logger, encoder) { }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("X-Test-Role", out var role))
            return Task.FromResult(AuthenticateResult.NoResult());
        var claims = new List<Claim> { new(ClaimTypes.Role, role.ToString()) };
        if (Request.Headers.TryGetValue("X-Test-User", out var id))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, id.ToString()));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
    }
}
