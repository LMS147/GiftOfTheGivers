using System.Reflection;
using GiftOfTheGivers.Controllers;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace GiftOfTheGivers.Tests;

public class DonorDashboardTests
{
    [Fact]
    public async Task OverviewUsesOnlyOwnersContributionsProjectsAndNotifications()
    {
        await using var db = Database();
        db.ReliefProjects.AddRange(new ReliefProject { Id = 1, Name = "Owner project" }, new ReliefProject { Id = 2, Name = "Other project" });
        db.Donations.AddRange(
            new Donation { DonorId = "owner", DonorName = "Owner name", DonationDate = DateTime.UtcNow.AddMinutes(-1), Currency = "ZAR", Amount = 100, ReliefProjectId = 1 },
            new Donation { DonorId = "owner", DonorName = "Owner name", DonationDate = DateTime.UtcNow.AddMinutes(-1), Currency = "USD", Amount = 20 },
            new Donation { DonorId = "other", Currency = "ZAR", Amount = 9000, ReliefProjectId = 2 });
        db.Notifications.AddRange(
            new Notification { UserId = "owner", Email = "owner@example.com", Subject = "Owner notice", Body = "Private" },
            new Notification { UserId = "other", Email = "other@example.com", Subject = "Other notice", Body = "Private" });
        await db.SaveChangesAsync();
        var model = await new DonorDashboardService(db).BuildAsync("owner", "ZAR", "account@example.com");
        Assert.Equal("Owner name", model.DisplayName);
        Assert.Equal(2, model.ContributionCount);
        Assert.Equal(100, model.SelectedTotal.Total);
        Assert.Equal(20, model.Totals.Single(t => t.Currency == "USD").Total);
        Assert.Equal(1, model.SupportedProjectCount);
        Assert.Equal("Owner project", Assert.Single(model.SupportedProjects).Name);
        Assert.Equal(1, model.UnreadNotificationCount);
        Assert.Equal("Owner notice", Assert.Single(model.Notifications).Subject);
        Assert.All(model.RecentContributions, d => Assert.Equal("owner", d.DonorId));
        Assert.Equal(100, model.Activity.Sum(m => m.Amount));
    }
    [Fact]
    public async Task EmptyOverviewHasSixZeroMonthsAndSafeCurrencyFallback()
    {
        await using var db = Database();
        var model = await new DonorDashboardService(db).BuildAsync("new-user", "invalid", "new@example.com");
        Assert.Equal("new", model.DisplayName); Assert.Equal("ZAR", model.Currency);
        Assert.Equal(0, model.SelectedTotal.Total); Assert.Null(model.LatestContribution);
        Assert.Equal(6, model.Activity.Count); Assert.All(model.Activity, m => Assert.Equal(0, m.Amount));
        Assert.Equal(model.Activity.OrderBy(m => m.Month).Select(m => m.Month), model.Activity.Select(m => m.Month));
    }
    [Fact]
    public void OverviewRequiresDonorRole()
    {
        var auth = Assert.Single(typeof(DonorController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal("Donor", auth.Roles);
    }
    private static ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
