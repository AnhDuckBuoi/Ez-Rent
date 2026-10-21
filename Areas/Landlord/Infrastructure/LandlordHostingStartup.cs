using EZ_Rent.Areas.Landlord.Infrastructure;
using EZ_Rent.Areas.Landlord.Services;
using EZ_Rent.Areas.Tenant.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: HostingStartup(typeof(LandlordHostingStartup))]

namespace EZ_Rent.Areas.Landlord.Infrastructure;

/// <summary>
/// Đăng ký phần mở rộng của khu vực Landlord mà không sửa Program.cs.
/// ASP.NET Core tự chạy các IHostingStartup khai báo trong assembly của ứng dụng khi khởi động.
/// 1) Bảo vệ/chuyển hướng các route CRUD tự sinh cũ của Landlord.
/// 2) Cung cấp khung giờ chủ trọ đã mở cho trang đặt lịch của Tenant (ITenantViewingScheduleReader).
///    Dùng TryAdd: nếu sau này Program.cs đăng ký một reader khác thì bản đăng ký sau được ưu tiên.
/// Muốn tắt cả hai: xóa file này (các trang Landlord mới vẫn chạy bình thường).
/// </summary>
public sealed class LandlordHostingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder.ConfigureServices(services =>
    {
        services.Configure<MvcOptions>(options => options.Conventions.Add(new LegacyLandlordRouteConvention()));
        services.TryAddScoped<ITenantViewingScheduleReader, LandlordViewingScheduleReader>();
    });
}
