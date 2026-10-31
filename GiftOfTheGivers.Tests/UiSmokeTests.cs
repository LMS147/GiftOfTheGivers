using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GiftOfTheGivers.Controllers;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
namespace GiftOfTheGivers.Tests;

public class UiSmokeTests
{
    [Fact]
    public async Task MainScreensRenderAndDonationReviewConfirmDownloadWorkOverHttp()
    {
        var dbName = Guid.NewGuid().ToString();
        using var server = new TestServer(new WebHostBuilder().ConfigureServices(services =>
        {
            services.AddLogging(); services.AddDataProtection(); services.AddHttpClient();
            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
            services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultUI();
            services.AddAuthentication(o => { o.DefaultScheme = "test"; o.DefaultAuthenticateScheme = "test"; o.DefaultChallengeScheme = "test"; o.DefaultForbidScheme = "test"; }).AddScheme<AuthenticationSchemeOptions, UiAuthentication>("test", _ => { });
            services.AddAuthorization(); services.AddScoped<ExperienceService>(); services.AddSingleton<SubmissionAccessService>(); services.AddSingleton<DonationReviewService>();
            services.AddHttpClient<ITaxCertificateService, TaxCertificateService>();
            services.AddControllersWithViews().AddApplicationPart(typeof(DonationsController).Assembly);
            services.AddRazorPages().AddApplicationPart(typeof(DonationsController).Assembly);
        }).Configure(app =>
        {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/test/token")
                { var token = context.RequestServices.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context); await context.Response.WriteAsync(token.RequestToken!); return; }
                await next();
            });
            app.UseEndpoints(e =>
            {
                e.MapAreaControllerRoute("staff-projects", "Staff", "Staff/Projects/{action=Index}/{id?}", new { controller = "Projects" });
                e.MapAreaControllerRoute("staff", "Staff", "Staff/{action=Dashboard}/{id?}", new { controller = "Staff" });
                e.MapAreaControllerRoute("volunteer", "Volunteer", "Volunteer/{action=Dashboard}/{id?}", new { controller = "VolunteerDashboard" });
                e.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); e.MapRazorPages();
            });
        }));
        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ReliefProjects.AddRange(new ReliefProject { Id = 1, Name = "KZN Flood Relief", Location = "KwaZulu-Natal", Description = "Food parcels and clean water for communities affected by flooding.", Category = "Food", TargetAmount = 50000 }, new ReliefProject { Id = 2, Name = "Winter Warmth", Location = "Gauteng", Category = "Shelter", Description = "Blankets and warm meals for community shelters.", TargetAmount = 30000 }, new ReliefProject { Id = 3, Name = "Water for Communities", Location = "Eastern Cape", Category = "Water", Description = "Supporting clean water access.", TargetAmount = 40000 });
            db.Volunteers.Add(new Volunteer { Id = 1, FullName = "Test Volunteer", Email = "test@example.com", Phone = "071 000 0000", Availability = "Weekends", Skills = "First Aid, Driving", UserId = "volunteer" });
            db.Donations.Add(new Donation { Id = 1, DonorName = "Test donor", Email = "donor@example.com", DonorId = "donor", Amount = 250, Currency = "ZAR", ReliefProjectId = 1, ReferenceNumber = "DEMO-1" });
            await db.SaveChangesAsync();
        }
        var client = server.CreateClient();
        foreach (var screen in new[] { ("/", "home", ""), ("/Projects", "projects", ""), ("/Projects/Details/1", "project-detail", ""), ("/Donations/Create", "donate", ""), ("/Staff", "staff", "Staff"), ("/Staff/Volunteers", "applications", "Staff"), ("/Staff/Donations", "records", "Staff"), ("/Staff/Projects", "manage-projects", "Staff"), ("/Staff/Diagnostics", "diagnostics", "Staff"), ("/Donations/MyDonations", "donor", "Donor"), ("/Volunteer/MyApplication", "volunteer", "Volunteer") })
        {
            client.DefaultRequestHeaders.Remove("X-Test-Role");
            if (screen.Item3 != "") client.DefaultRequestHeaders.Add("X-Test-Role", screen.Item3);
            var response = await client.GetAsync(screen.Item1);
            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{screen.Item1}: {response.StatusCode} -> {response.Headers.Location}");
            var html = await response.Content.ReadAsStringAsync(); Assert.Contains("main-content", html);
            var output = Environment.GetEnvironmentVariable("UI_SNAPSHOT_DIRECTORY");
            if (output != null) { Directory.CreateDirectory(output); await File.WriteAllTextAsync(Path.Combine(output, screen.Item2 + ".html"), html); }
        }
        client.DefaultRequestHeaders.Remove("X-Test-Role");
        var tokenResponse = await client.GetAsync("/test/token"); var token = await tokenResponse.Content.ReadAsStringAsync();
        client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", tokenResponse.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        var review = await client.PostAsync("/Donations/Review", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["__RequestVerificationToken"] = token, ["DonorName"] = "Guest donor", ["Email"] = "guest@example.com", ["Amount"] = "125.50", ["Currency"] = "USD", ["ReliefProjectId"] = "1", ["SubmissionKey"] = Guid.NewGuid().ToString() }));
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var reviewHtml = await review.Content.ReadAsStringAsync(); Assert.Contains("USD 125.50", reviewHtml);
        var reviewToken = System.Text.RegularExpressions.Regex.Match(reviewHtml, "name=\"reviewToken\" value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(reviewToken);
        var submitted = await client.PostAsync("/Donations/Create", new FormUrlEncodedContent(new Dictionary<string,string> { ["__RequestVerificationToken"] = token, ["reviewToken"] = WebUtility.HtmlDecode(reviewToken) }));
        Assert.Equal(HttpStatusCode.Redirect, submitted.StatusCode);
        var location = submitted.Headers.Location!.ToString();
        var cookie = string.Join("; ", submitted.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]));
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", cookie);
        var confirmation = await client.GetAsync(location); Assert.Equal(HttpStatusCode.OK, confirmation.StatusCode);
        Assert.DoesNotContain("Azure Function Response", await confirmation.Content.ReadAsStringAsync());
        var id = location.Split('/').Last();
        var pdf = await client.GetAsync("/Donations/Download/" + id); Assert.Equal("application/pdf", pdf.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(await pdf.Content.ReadAsByteArrayAsync()));
    }
    private sealed class UiAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var role = Request.Headers["X-Test-Role"].ToString();
            if (string.IsNullOrEmpty(role)) return Task.FromResult(AuthenticateResult.NoResult());
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, role.ToLowerInvariant()), new Claim(ClaimTypes.Name, role.ToLowerInvariant()+"@example.com"), new Claim(ClaimTypes.Role, role) }, IdentityConstants.ApplicationScheme));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, "test")));
        }
    }
}
