using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using GiftOfTheGivers.Controllers;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Helpers.Models.ViewModels;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace GiftOfTheGivers.Tests;

public class ExperienceTests
{
    [Fact]
    public void ReviewTokenRejectsTamperingAndDifferentAccount()
    {
        var reviews = new DonationReviewService(new EphemeralDataProtectionProvider());
        var input = ValidInput();
        var token = reviews.Protect(input, "donor-a");
        Assert.Equal(input.Amount, reviews.Read(token, "donor-a")!.Amount);
        Assert.Null(reviews.Read(token, "donor-b"));
        Assert.Null(reviews.Read("invalid", "donor-a"));
        Assert.Null(reviews.Read(token[..^10] + "tampered", "donor-a"));
    }
    [Fact]
    public async Task ReplayingReviewedDonationCreatesOneRecordAndOneNotification()
    {
        await using var db = Database();
        var provider = new EphemeralDataProtectionProvider();
        var reviews = new DonationReviewService(provider);
        var controller = new DonationsController(db, new SubmissionAccessService(provider), reviews, new ExperienceService(db), new CertificateStub())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var token = reviews.Protect(ValidInput(), "guest");
        Assert.IsType<RedirectToActionResult>(await controller.Create(token));
        Assert.IsType<RedirectToActionResult>(await controller.Create(token));
        Assert.Single(await db.Donations.ToListAsync());
        Assert.Single(await db.Notifications.ToListAsync());
    }
    [Fact]
    public async Task ClosedProjectIsRejectedAtFinalConfirmation()
    {
        await using var db = Database();
        db.ReliefProjects.Add(new ReliefProject { Id = 1, Name = "Closed", IsActive = false }); await db.SaveChangesAsync();
        var provider = new EphemeralDataProtectionProvider(); var reviews = new DonationReviewService(provider);
        var controller = new DonationsController(db, new SubmissionAccessService(provider), reviews, new ExperienceService(db), new CertificateStub())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }, TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(new DefaultHttpContext(), new MemoryTempData()) };
        var input = ValidInput(); input.ReliefProjectId = 1;
        Assert.IsType<RedirectToActionResult>(await controller.Create(reviews.Protect(input, "guest")));
        Assert.Empty(await db.Donations.ToListAsync());
    }
    [Fact]
    public async Task CurrencyTotalsDoNotCombineCurrenciesOrOtherDonors()
    {
        await using var db = Database();
        db.Donations.AddRange(new Donation { DonorId = "mine", Amount = 100, Currency = "ZAR" }, new Donation { DonorId = "mine", Amount = 20, Currency = "USD" }, new Donation { DonorId = "other", Amount = 5000 });
        await db.SaveChangesAsync();
        var provider = new EphemeralDataProtectionProvider();
        var controller = new DonationsController(db, new SubmissionAccessService(provider), new DonationReviewService(provider), new ExperienceService(db), new CertificateStub())
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Account("mine") } } };
        var result = Assert.IsType<ViewResult>(await controller.MyDonations());
        var model = Assert.IsType<DonorDashboardViewModel>(result.Model);
        Assert.Equal(2, model.ContributionCount); Assert.Equal(100, model.Totals.Single(x => x.Currency == "ZAR").Total); Assert.Equal(20, model.Totals.Single(x => x.Currency == "USD").Total);
    }
    [Fact]
    public async Task NotificationReadCannotChangeAnotherUsersMessage()
    {
        await using var db = Database();
        db.Notifications.Add(new Notification { Id = 1, UserId = "owner", Email = "test@example.com", Subject = "Test", Body = "Test" }); await db.SaveChangesAsync();
        var controller = new NotificationsController(db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = Account("other") } } };
        Assert.IsType<NotFoundResult>(await controller.MarkRead(1));
        Assert.Null((await db.Notifications.FindAsync(1))!.ReadAtUtc);
    }
    [Fact]
    public async Task PaginationClampsOutOfRangePageAndKeepsTotal()
    {
        await using var db = Database();
        for (int i = 0; i < 25; i++) db.ReliefProjects.Add(new ReliefProject { Name = "Project " + i });
        await db.SaveChangesAsync();
        var page = await ExperienceService.PageAsync(db.ReliefProjects.OrderBy(p => p.Id), 100);
        Assert.Equal(3, page.Page); Assert.Equal(25, page.TotalCount); Assert.Single(page.Items);
    }
    [Fact]
    public void ReceiptPdfHasValidObjectOffsetsAndPrototypeLabel()
    {
        var pdf = ReceiptPdfService.Generate(new Donation { DonorName = "A (test) \\ person", Email = "test@example.com", Amount = 50, ReferenceNumber = "DEMO-1" });
        var text = Encoding.ASCII.GetString(pdf);
        Assert.StartsWith("%PDF-1.4", text); Assert.Contains("NO PAYMENT PROCESSED", text);
        var offset = int.Parse(text.Split("startxref\n")[1].Split('\n')[0]);
        Assert.StartsWith("xref", text[offset..]);
        Assert.Contains("A \\(test\\) \\\\ person", text);
    }
    private static DonationInput ValidInput() => new() { DonorName = "Test donor", Email = "test@example.com", Amount = 100, Currency = "ZAR" };
    private static ClaimsPrincipal Account(string id) => new(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id) }, "test"));
    private static ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private sealed class CertificateStub : ITaxCertificateService
    { public string FunctionEndpointUrl => ""; public Task<TaxCertificateResult> GenerateTaxCertificateAsync(Donation donation, CancellationToken cancellationToken = default) => Task.FromResult(new TaxCertificateResult { Success = true }); }
    private sealed class MemoryTempData : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    { public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>(); public void SaveTempData(HttpContext context, IDictionary<string, object> values) { } }
}
