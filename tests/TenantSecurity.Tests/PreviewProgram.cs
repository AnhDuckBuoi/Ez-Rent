namespace TenantSecurity.Tests;

public static class PreviewProgram
{
    public static async Task Main(string[] args)
    {
        if (!args.Contains("--preview"))
        {
            Console.WriteLine("Dùng dotnet test để kiểm thử, hoặc thêm --preview để chạy giao diện với dữ liệu mẫu riêng.");
            return;
        }
        var host = new TenantTestApp();
        await host.StartPreviewAsync();
        Console.WriteLine("Preview: http://127.0.0.1:" + (Environment.GetEnvironmentVariable("EZRENT_PREVIEW_PORT") ?? "8770") + "/review/tenant — dữ liệu mẫu riêng, không dùng database nhóm.");
        try { await host.WaitForPreviewShutdownAsync(); }
        finally { await host.DisposeAsync(); }
    }
}
