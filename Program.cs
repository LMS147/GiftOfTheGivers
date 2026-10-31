using GiftOfTheGivers.Data;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

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

    // Prevents EF Core 9 from throwing 'PendingModelChangesWarning' crashes
    options.ConfigureWarnings(warnings =>
        warnings.Ignore(RelationalEventId.PendingModelChangesWarning));
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
builder.Services.AddHttpClient<ITaxCertificateService, TaxCertificateService>();

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

    await DbSeeder.SeedAsync(services);
}

app.Run();
