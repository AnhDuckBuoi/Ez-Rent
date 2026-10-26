using EZ_Rent.Data;
using EZ_Rent.Models;
using EZ_Rent.Services;
using EZ_Rent.Data.Seeder;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
var datasetDemo = builder.Environment.IsEnvironment("Demo");
var demoDatabase = datasetDemo ? DatasetDemoSeed.DatabaseName(builder.Configuration.GetConnectionString("DefaultConnection")) : null;

// ============================================================
// SERVICES
// ============================================================
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddSignalR(options => options.MaximumReceiveMessageSize = 16384);
builder.Services.AddScoped<ChatRealtime>();
builder.Services.AddScoped<ChatService>();
builder.Services.AddOptions<EZ_Rent.Services.RentalFileStorageOptions>()
    .Bind(builder.Configuration.GetSection("RentalFileStorage"));
if (datasetDemo)
{
    builder.Services.ConfigureApplicationCookie(options => options.Cookie.Name = "EZRent.DatasetDemo." + demoDatabase);
    builder.Services.Configure<RentalFileStorageOptions>(options => options.RootPath ??=
        Path.Combine(builder.Environment.ContentRootPath, "App_Data", "Demo", demoDatabase!, "RentalFiles"));
}
builder.Services.AddSingleton<EZ_Rent.Services.RentalPrivateFileStore>();
builder.Services.AddScoped<EZ_Rent.Services.RentalDocumentService>();
builder.Services.AddScoped<EZ_Rent.Areas.Tenant.Services.TenantContractDraftService>();
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

if (!builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>(optional: true);
}

var authentication = builder.Services.AddAuthentication();
var googleClientId = string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientId"])
    ? "ezrent-google-client-id"
    : builder.Configuration["Authentication:Google:ClientId"]!;
var googleClientSecret = string.IsNullOrWhiteSpace(builder.Configuration["Authentication:Google:ClientSecret"])
    ? "ezrent-google-client-secret"
    : builder.Configuration["Authentication:Google:ClientSecret"]!;

authentication.AddGoogle(options =>
{
    options.ClientId = googleClientId;
    options.ClientSecret = googleClientSecret;

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
app.MapHub<ChatHub>("/hubs/chat", options => options.CloseOnAuthenticationExpiration = true);

// ============================================================
// SEED DATA
// ============================================================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await DbSeeder.SeedAsync(scope.ServiceProvider);
    if (datasetDemo)
    {
        var demoInfo = await DatasetDemoSeed.PrepareAsync(scope.ServiceProvider, app.Environment);
        app.MapGet("/review/dataset", () => Results.Json(demoInfo)).AllowAnonymous();
        app.MapGet("/review/tenant", async (HttpContext context) =>
        {
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.SendFileAsync(Path.Combine(app.Environment.ContentRootPath, "docs", "tenant-app-responsive-review.html"));
        }).AllowAnonymous();
    }
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
