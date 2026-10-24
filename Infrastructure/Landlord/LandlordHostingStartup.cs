using EZ_Rent.Areas.Tenant.Services;
using EZ_Rent.Infrastructure.Landlord;
using EZ_Rent.Services.Viewings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

[assembly: HostingStartup(typeof(LandlordHostingStartup))]

namespace EZ_Rent.Infrastructure.Landlord;

/// <summary>
/// Đăng ký phần chủ trọ mà không sửa Program.cs (ASP.NET Core tự chạy các IHostingStartup trong assembly của ứng dụng):
/// 1) LegacyLandlordRouteConvention: khóa các controller CRUD tự sinh cũ của Landlord và chuyển sang trang mới.
/// 2) ViewingScheduleReader: nguồn khung giờ chủ trọ mở cho trang đặt lịch của Tenant (ITenantViewingScheduleReader).
///    Dùng TryAdd: nếu Program.cs đăng ký một reader khác thì bản của Program.cs được dùng.
/// Các dịch vụ dùng chung của nhóm (RentalPrivateFileStore, RentalDocumentService) đã đăng ký sẵn trong Program.cs.
/// </summary>
public sealed class LandlordHostingStartup : IHostingStartup
{
    public void Configure(IWebHostBuilder builder) => builder.ConfigureServices(services =>
    {
        services.Configure<MvcOptions>(options => options.Conventions.Add(new LegacyLandlordRouteConvention()));
        services.TryAddScoped<ITenantViewingScheduleReader, ViewingScheduleReader>();
    });
}
