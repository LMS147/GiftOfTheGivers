using System.Net;
using System.Text.RegularExpressions;
using GiftOfTheGivers.Controllers;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace GiftOfTheGivers.Tests;

public class StaffLoginTests
{
    [Fact]
    public async Task ProvisionedStaffCanSignInAndPublicRegistrationCannotChooseStaff()
    {
        var name = Guid.NewGuid().ToString();
        using var server = new TestServer(new WebHostBuilder().UseSetting(WebHostDefaults.ApplicationKey, typeof(DonationsController).Assembly.GetName().Name!).ConfigureServices(services =>
        {
            services.AddLogging(); services.AddDataProtection(); services.AddHttpClient();
            services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(name));
            services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultUI().AddDefaultTokenProviders();
            services.AddScoped<StaffBootstrapper>(); services.AddScoped<ExperienceService>();
            services.AddControllersWithViews().AddApplicationPart(typeof(DonationsController).Assembly);
            services.AddRazorPages().AddApplicationPart(typeof(DonationsController).Assembly)
                .ConfigureApplicationPartManager(parts =>
                {
                    // The app's scaffolded pages override the Identity UI library, as in production.
                    var ownParts = parts.ApplicationParts.Where(p => p.Name == typeof(DonationsController).Assembly.GetName().Name).ToList();
                    foreach (var part in ownParts) parts.ApplicationParts.Remove(part);
                    foreach (var part in ownParts.AsEnumerable().Reverse()) parts.ApplicationParts.Insert(0, part);
                });
        }).Configure(app =>
        {
            app.UseRouting(); app.UseAuthentication(); app.UseAuthorization();
            app.UseEndpoints(e => { e.MapAreaControllerRoute("staff", "Staff", "Staff/{action=Dashboard}/{id?}", new { controller = "Staff" }); e.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}"); e.MapRazorPages(); });
        }));
        const string password = "Private!StaffLogin2026";
        using (var scope = server.Services.CreateScope())
        {
            var result = await scope.ServiceProvider.GetRequiredService<StaffBootstrapper>().InitializeAsync(new() { Enabled = true, Email = "staff@example.com", Password = password });
            Assert.Equal(StaffBootstrapStatus.Created, result.Status);
        }
        using (var scope = server.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            Assert.True(await users.IsInRoleAsync((await users.FindByEmailAsync("staff@example.com"))!, "Staff"));
        }
        var client = server.CreateClient();
        var loginHtml = await client.GetStringAsync("/Identity/Account/Login");
        Assert.Contains("Welcome back", loginHtml);
        var token = await TokenAsync(client, "/Identity/Account/Login");
        var response = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["__RequestVerificationToken"] = token, ["Input.Email"] = "staff@example.com", ["Input.Password"] = password }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode); Assert.StartsWith("/Staff", response.Headers.Location!.ToString());
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        var dashboard = await client.GetAsync(response.Headers.Location); Assert.Equal(HttpStatusCode.OK, dashboard.StatusCode);
        var guest = server.CreateClient(); var registrationToken = await TokenAsync(guest, "/Identity/Account/Register");
        var registration = await guest.PostAsync("/Identity/Account/Register", new FormUrlEncodedContent(new Dictionary<string,string>
        { ["__RequestVerificationToken"] = registrationToken, ["Input.Email"] = "public@example.com", ["Input.Password"] = password, ["Input.ConfirmPassword"] = password, ["Input.Role"] = "Staff" }));
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        using var verify = server.Services.CreateScope();
        Assert.Null(await verify.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>().FindByEmailAsync("public@example.com"));
    }
    private static async Task<string> TokenAsync(HttpClient client, string url)
    {
        var response = await client.GetAsync(url); Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        Assert.NotEmpty(token);
        client.DefaultRequestHeaders.Remove("Cookie"); client.DefaultRequestHeaders.Add("Cookie", string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0])));
        return WebUtility.HtmlDecode(token);
    }
}
