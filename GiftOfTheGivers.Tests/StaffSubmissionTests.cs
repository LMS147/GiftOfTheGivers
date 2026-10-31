using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using GiftOfTheGivers.Areas.Staff.Controllers;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace GiftOfTheGivers.Tests;

public class StaffSubmissionTests
{
    [Fact]
    public async Task PostedFormBindsNewUpdateFieldsAndIgnoresSpoofedMetadata()
    {
        var dbName = Guid.NewGuid().ToString();
        using var server = new TestServer(new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(dbName));
                services.AddHttpClient();
                services.AddAuthentication("test").AddScheme<AuthenticationSchemeOptions, StaffAuthentication>("test", _ => { });
                services.AddAuthorization();
                // This test isolates MVC binding and the save action; CSRF remains enabled in production.
                services.AddControllersWithViews(o => o.Filters.Add(new IgnoreAntiforgeryTokenAttribute()))
                    .AddApplicationPart(typeof(StaffController).Assembly);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseAuthentication();
                app.UseAuthorization();
                app.UseEndpoints(e => e.MapAreaControllerRoute("staff", "Staff", "Staff/{action}", new { controller = "Staff" }));
            }));
        using (var scope = server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.ReliefProjects.Add(new ReliefProject { Id = 1, Name = "Test project" });
            await db.SaveChangesAsync();
        }
        var response = await server.CreateClient().PostAsync("/Staff/PostUpdate", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["NewUpdate.ReliefProjectId"] = "1", ["NewUpdate.Title"] = "Delivered supplies",
            ["NewUpdate.Body"] = "Delivery complete", ["NewUpdate.Id"] = "999",
            ["NewUpdate.PostedByName"] = "spoofed", ["NewUpdate.PostedAt"] = "2000-01-01"
        }));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var verifyScope = server.Services.CreateScope();
        var saved = Assert.Single(await verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>().ProjectUpdates.ToListAsync());
        Assert.Equal("Delivered supplies", saved.Title);
        Assert.Equal("Delivery complete", saved.Body);
        Assert.Equal("staff@example.com", saved.PostedByName);
        Assert.NotEqual(999, saved.Id);
        Assert.True(saved.PostedAt.Year > 2000);
    }

    private sealed class StaffAuthentication : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public StaffAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder) { }
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "staff"), new Claim(ClaimTypes.Name, "staff@example.com"),
                new Claim(ClaimTypes.Role, "Staff")
            }, "test"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(user, "test")));
        }
    }
}
