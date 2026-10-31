using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// 1. Database context — SQL Server.
//    • Azure App Service : the "DefaultConnection" connection string set in
//      App Service -> Configuration -> Connection strings (type SQLAzure)
//      overrides this file.
//    • Local development : LocalDB from appsettings.Development.json.
// ---------------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);

});

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// ---------------------------------------------------------------------------
// 2. ASP.NET Core Identity — users (IdentityUser) and roles (Staff, Volunteer, Donor).
// ---------------------------------------------------------------------------
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 6;
    options.Password.RequireNonAlphanumeric = false;
})
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultUI()
    .AddDefaultTokenProviders();

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ---------------------------------------------------------------------------
// 3. Section A / D: Azure Functions HTTP client & Helper integration
// ---------------------------------------------------------------------------
builder.Services.AddDataProtection();
builder.Services.AddSingleton<SubmissionAccessService>();
builder.Services.AddSingleton<DonationReviewService>();
builder.Services.AddScoped<ExperienceService>();
builder.Services.AddScoped<DonorDashboardService>();
builder.Services.AddScoped<StaffBootstrapper>();
builder.Services.Configure<EmailOptions>(builder.Configuration.GetSection("Email"));
builder.Services.AddTransient<INotificationEmailSender, SmtpNotificationEmailSender>();
builder.Services.AddHostedService<EmailNotificationWorker>();
builder.Services.AddHttpClient("GiftOfTheGiversFunctions", client =>
{
    var url = builder.Configuration["AzureFunctions:BaseUrl"];
    if (!string.IsNullOrWhiteSpace(url))
    {
        client.BaseAddress = new Uri(url.TrimEnd('/') + "/");
    }
    client.Timeout = TimeSpan.FromSeconds(10);
    var key = builder.Configuration["AzureFunctions:FunctionKey"];
    if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("x-functions-key", key);
});
builder.Services.AddHttpClient<ITaxCertificateService, TaxCertificateService>(client =>
{
    var key = builder.Configuration["AzureFunctions:FunctionKey"];
    if (!string.IsNullOrWhiteSpace(key)) client.DefaultRequestHeaders.Add("x-functions-key", key);
});

var app = builder.Build();

// ---------------------------------------------------------------------------
// 3. Request pipeline
// ---------------------------------------------------------------------------
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapAreaControllerRoute("staff-projects", "Staff", "Staff/Projects/{action=Index}/{id?}", new { controller = "Projects" });
app.MapAreaControllerRoute(
    name: "staff",
    areaName: "Staff",
    pattern: "Staff/{action=Dashboard}/{id?}",
    defaults: new { controller = "Staff" });
app.MapAreaControllerRoute(
    name: "volunteer",
    areaName: "Volunteer",
    pattern: "Volunteer/{action=Dashboard}/{id?}",
    defaults: new { controller = "VolunteerDashboard" });

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

// ---------------------------------------------------------------------------
// 4. Create the schema, then seed roles, demo accounts and starting data.
//    MigrateAsync() applies the EF migrations in the Migrations/ folder,
//    which creates every table (AspNet* + Donations, Volunteers,
//    ReliefProjects, ProjectUpdates) — on Azure SQL and on LocalDB alike.
// ---------------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();

    // Create all tables (AspNet* + Donations, Volunteers, ReliefProjects,
    // ProjectUpdates) from the EF migrations, then seed roles + demo data.
    await context.Database.MigrateAsync();

    await DbSeeder.SeedAsync(services, app.Environment.IsDevelopment() && builder.Configuration.GetValue<bool>("SeedDemoData"));

    // Production staff setup is explicitly configured through deployment settings.
    await services.GetRequiredService<StaffBootstrapper>().InitializeAsync(
        builder.Configuration.GetSection("StaffBootstrap").Get<StaffBootstrapOptions>() ?? new());
}

app.Run();

