using EZ_Rent.Data;
using EZ_Rent.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authentication;
var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddOptions<EZ_Rent.Services.RentalFileStorageOptions>();
builder.Services.AddSingleton<EZ_Rent.Services.RentalPrivateFileStore>();
builder.Services.AddScoped<EZ_Rent.Services.RentalDocumentService>();
builder.Services.AddScoped<EZ_Rent.Areas.Tenant.Services.TenantContractDraftService>();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddTransient<
    Microsoft.AspNetCore.Identity.UI.Services.IEmailSender,
    EmailSender>();
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    // ===== KHÓA TÀI KHOẢN KHI ĐĂNG NHẬP SAI NHIỀU LẦN =====
    options.Lockout.MaxFailedAccessAttempts = 5;              // Sai 5 lần → khóa
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);  // Khóa 15 phút
    options.Lockout.AllowedForNewUsers = true;                // Áp dụng cả user mới

    // ===== XÁC THỰC TÀI KHOẢN =====
    options.SignIn.RequireConfirmedAccount = false;   // Bật true nếu có SMTP thật
    options.SignIn.RequireConfirmedEmail = true;     // Bật true nếu bắt verify email
    options.SignIn.RequireConfirmedPhoneNumber = false;

    // ===== ĐĂNG NHẬP =====
    options.User.RequireUniqueEmail = true;           // Email không trùng
    options.User.AllowedUserNameCharacters =           // Chỉ cho ký tự này trong username
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId =
            builder.Configuration["Authentication:Google:ClientId"]!;

        options.ClientSecret =
            builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.ClaimActions.MapJsonKey("urn:google:picture", "picture", "url");
        options.ClaimActions.MapJsonKey("urn:google:locale", "locale", "string");
        options.Scope.Add("profile");
        options.Scope.Add("email");
        options.Events.OnRemoteFailure = context =>
        {
            // Kiểm tra lỗi từ Google
            var error = context.Request.Query["error"].ToString();

            if (error == "access_denied")
            {
                // User bấm "Hủy" → redirect về trang Login với thông báo
                context.Response.Redirect("/Identity/Account/Login?error=cancelled");
                context.HandleResponse();   // 🔥 Quan trọng — chặn exception
            }
            else
            {
                // Lỗi khác → redirect về trang Login
                context.Response.Redirect("/Identity/Account/Login?error=failed");
                context.HandleResponse();
            }

            return Task.CompletedTask;
        };
    });
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Home}/{action=Index}/{id?}");
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider
        .GetRequiredService<RoleManager<IdentityRole>>();

    var userManager = scope.ServiceProvider
        .GetRequiredService<UserManager<ApplicationUser>>();

    // Tạo các Role
    string[] roles =
    {
        "Tenant",
        "Landlord",
        "Admin"
    };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            await roleManager.CreateAsync(new IdentityRole(role));
        }
    }

    // Tạo tài khoản Admin
    var admin = await userManager.FindByEmailAsync("admin@gmail.com");

    if (admin == null)
    {
        admin = new ApplicationUser
        {
            UserName = "admin@gmail.com",
            Email = "admin@gmail.com",
            EmailConfirmed = true,
            FullName = "Administrator",
            CreatedAt = DateTime.UtcNow
        };

        var result = await userManager.CreateAsync(admin, "Admin@123");

        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}
app.Run();
