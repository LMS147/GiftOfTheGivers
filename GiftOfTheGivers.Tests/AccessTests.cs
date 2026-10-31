using System.Security.Claims;
using GiftOfTheGivers.Controllers;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GiftOfTheGivers.Tests;

public class AccessTests
{
    private readonly EphemeralDataProtectionProvider _provider = new();
    private static ClaimsPrincipal User(string id, string role = "Donor") => new(new ClaimsIdentity(new[]
    { new Claim(ClaimTypes.NameIdentifier, id), new Claim(ClaimTypes.Role, role) }, "test"));
    private static ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public void OwnerAndStaffCanReadButOtherAccountsAndGuestsCannot()
    {
        var access = new SubmissionAccessService(_provider);
        var context = new DefaultHttpContext();
        Assert.False(access.CanRead(context, "donation", 1, "owner"));
        context.User = User("other");
        Assert.False(access.CanRead(context, "donation", 1, "owner"));
        context.User = User("owner");
        Assert.True(access.CanRead(context, "donation", 1, "owner"));
        context.User = User("staff", "Staff");
        Assert.True(access.CanRead(context, "donation", 1, "owner"));
    }

    [Fact]
    public void GuestGrantIsBoundToRecordTypeAndIdAndCannotReadAccountOwnedRecords()
    {
        var access = new SubmissionAccessService(_provider);
        var browser = new DefaultHttpContext();
        access.Grant(browser, "donation", 12);
        var cookie = browser.Response.Headers.SetCookie.ToString().Split(';')[0];
        var next = new DefaultHttpContext();
        next.Request.Headers.Cookie = cookie;
        Assert.True(access.CanRead(next, "donation", 12, null));
        Assert.False(access.CanRead(next, "donation", 13, null));
        Assert.False(access.CanRead(next, "volunteer", 12, null));
        Assert.False(access.CanRead(next, "donation", 12, "owner"));
        next.Request.Headers.Cookie = "gotg-donation-12=tampered";
        Assert.False(access.CanRead(next, "donation", 12, null));
    }

    [Fact]
    public void ExpiredGuestGrantIsRejected()
    {
        var token = _provider.CreateProtector("GiftOfTheGivers.SubmissionAccess.v1")
            .Protect($"donation:12:{DateTimeOffset.UtcNow.AddMinutes(-1).ToUnixTimeSeconds()}");
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = "gotg-donation-12=" + token;
        Assert.False(new SubmissionAccessService(_provider).CanRead(context, "donation", 12, null));
    }

    [Fact]
    public async Task BothCertificateEndpointsDenyAnotherDonorBeforeCallingCloudService()
    {
        await using var db = Database();
        db.Donations.Add(new Donation { Id = 1, DonorId = "owner" });
        await db.SaveChangesAsync();
        var controller = new DonationsController(db, new SubmissionAccessService(_provider), new DonationReviewService(_provider), new ExperienceService(db),
            new NeverCalledCertificateService())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = User("other") } } };
        Assert.IsType<NotFoundResult>(await controller.Certificate(1));
        Assert.IsType<NotFoundResult>(await controller.CloudCertificate(1));
    }

    [Fact]
    public async Task GuestCannotReadAnotherVolunteerConfirmation()
    {
        await using var db = Database();
        db.Volunteers.Add(new Volunteer { Id = 1, FullName = "Test", Email = "test@example.com", Phone = "123", Availability = "Weekends" });
        await db.SaveChangesAsync();
        var controller = new VolunteersController(new SubmissionAccessService(_provider), db, new ExperienceService(db))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        Assert.IsType<NotFoundResult>(await controller.ThankYou(1));
    }

    [Theory]
    [InlineData("Index")][InlineData("Details")][InlineData("Delete")][InlineData("DeleteConfirmed")]
    public void DonationManagementRequiresStaff(string action)
    {
        var attribute = Assert.Single(typeof(DonationsController).GetMethod(action)!
            .GetCustomAttributes(typeof(AuthorizeAttribute), true).Cast<AuthorizeAttribute>());
        Assert.Equal("Staff", attribute.Roles);
    }

    private sealed class Clients : IHttpClientFactory
    { public HttpClient CreateClient(string name) => new(); }
    private sealed class NeverCalledCertificateService : ITaxCertificateService
    {
        public string FunctionEndpointUrl => "https://example.invalid";
        public Task<TaxCertificateResult> GenerateTaxCertificateAsync(Donation donation, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Unauthorized requests must not reach the cloud service.");
    }
}
