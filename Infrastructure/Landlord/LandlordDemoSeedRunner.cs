using EZ_Rent.Data.Seeder;

namespace EZ_Rent.Infrastructure.Landlord;

/// <summary>
/// Chạy LandlordDemoSeed một lần khi ứng dụng khởi động, SAU phần migrate + DbSeeder trong Program.cs
/// (hosted service chỉ bắt đầu khi app.Run()). Không sửa Program.cs.
/// Bật/tắt: mặc định chỉ chạy ở môi trường Development và Demo; đặt "LandlordDemo:Seed" = true/false trong cấu hình để ép bật/tắt.
/// Lỗi khi tạo dữ liệu mẫu chỉ được ghi log, không làm ứng dụng dừng.
/// </summary>
public sealed class LandlordDemoSeedRunner(IServiceProvider services, IHostEnvironment environment, IConfiguration configuration,
    ILogger<LandlordDemoSeedRunner> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = configuration.GetValue<bool?>("LandlordDemo:Seed")
            ?? (environment.IsDevelopment() || environment.IsEnvironment("Demo"));
        if (!enabled) return;
        try
        {
            using var scope = services.CreateScope();
            var replaced = await DemoRoomImages.ReplaceRemoteAsync(scope.ServiceProvider.GetRequiredService<EZ_Rent.Data.AppDbContext>(), cancellationToken);
            if (replaced > 0) logger.LogInformation("Đã đổi {Count} ảnh picsum.photos sang ảnh minh họa trong dự án.", replaced);
            await LandlordDemoSeed.SeedAsync(scope.ServiceProvider, logger, cancellationToken);
            await MarketplaceDemoSeed.SeedAsync(scope.ServiceProvider, logger, cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không tạo được dữ liệu mẫu cho chủ trọ.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
