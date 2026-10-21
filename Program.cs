using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Services;
using EZ_Rent.Data.Seeder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// ============================================================
// SERVICES
// ============================================================
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddTransient<
    Microsoft.AspNetCore.Identity.UI.Services.IEmailSender,
    EmailSender>();

builder.Services.AddMemoryCache();
builder.Services.AddScoped<RecommendationService>();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    // ===== KHÓA TÀI KHOẢN KHI ĐĂNG NHẬP SAI NHIỀU LẦN =====
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
    options.Lockout.AllowedForNewUsers      = true;

    // ===== XÁC THỰC TÀI KHOẢN =====
    options.SignIn.RequireConfirmedAccount     = false;
    options.SignIn.RequireConfirmedEmail       = false;
    options.SignIn.RequireConfirmedPhoneNumber = false;

    // ===== ĐĂNG NHẬP =====
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._@+";
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<AppDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId     = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;

        options.ClaimActions.MapJsonKey("urn:google:picture", "picture", "url");
        options.ClaimActions.MapJsonKey("urn:google:locale", "locale", "string");

        options.Scope.Add("profile");
        options.Scope.Add("email");

        options.Events.OnRemoteFailure = context =>
        {
            var error = context.Request.Query["error"].ToString();

            if (error == "access_denied")
                context.Response.Redirect("/Identity/Account/Login?error=cancelled");
            else
                context.Response.Redirect("/Identity/Account/Login?error=failed");

            context.HandleResponse();
            return Task.CompletedTask;
        };
    });

var app = builder.Build();

// ============================================================
// PIPELINE
// ============================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
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

// ============================================================
// SEED DATA
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
}

// ============================================================
// RECOMMENDER EVALUATION — chỉ chạy khi flag = true
// ============================================================
if (builder.Configuration.GetValue<bool>("EnableRecommenderEvaluation"))
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Console.WriteLine("=== STARTING EVALUATION ===");

        var interactions = await RecommendationService.GetAllInteractionsAsync(db);
        Console.WriteLine($"Total interactions: {interactions.Count}");

        if (interactions.Count >= 10)
        {
            var result = RecommenderEvaluator.Evaluate(interactions, trainRatio: 0.8, topK: 20);

            Console.WriteLine("=== EVALUATION RESULT ===");
            Console.WriteLine($"Train size:       {result.TrainSize}");
            Console.WriteLine($"Test size:        {result.TestSize}");
            Console.WriteLine($"Users evaluated:  {result.UsersEvaluated}");
            Console.WriteLine($"Hits @ {result.K}:        {result.Hits}");
            Console.WriteLine($"Precision@{result.K}:    {result.PrecisionAtK:P2}");
            Console.WriteLine($"Recall@{result.K}:       {result.RecallAtK:P2}");
            Console.WriteLine($"Hit Rate@{result.K}:     {result.HitRateAtK:P2}");
        }
        else
        {
            Console.WriteLine("Not enough interactions to evaluate");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("=== EVALUATION FAILED ===");
        Console.WriteLine($"Error: {ex.Message}");
        Console.WriteLine($"Stack: {ex.StackTrace}");
    }
}

app.Run();