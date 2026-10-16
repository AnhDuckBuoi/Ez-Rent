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
        Console.WriteLine("Preview Tenant: http://127.0.0.1:8770/Tenant/RentalPost và /Tenant/ViewingAppointment — dữ liệu mẫu trong bộ nhớ, không dùng database nhóm.");
        try { await host.WaitForPreviewShutdownAsync(); }
        finally { await host.DisposeAsync(); }
    }
}
