using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models.ViewModels;
using GiftOfTheGivers.Models;
using Microsoft.EntityFrameworkCore;
namespace GiftOfTheGivers.Services;

public sealed class DonorDashboardService(ApplicationDbContext db)
{
    public async Task<DonorOverviewViewModel> BuildAsync(string userId, string? requestedCurrency, string? accountName)
    {
        var mine = db.Donations.AsNoTracking().Where(d => d.DonorId == userId);
        var recent = await mine.Include(d => d.ReliefProject).OrderByDescending(d => d.DonationDate).ThenByDescending(d => d.Id).Take(5).ToListAsync();
        var totals = await mine.GroupBy(d => d.Currency).Select(g => new CurrencySummary { Currency = g.Key, Total = g.Sum(d => d.Amount), Count = g.Count() }).OrderBy(t => t.Currency).ToListAsync();
        var currency = requestedCurrency is "ZAR" or "USD" or "EUR" ? requestedCurrency : recent.FirstOrDefault()?.Currency ?? "ZAR";
        var now = DateTime.UtcNow;
        var firstMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-5);
        var activity = await mine.Where(d => d.Currency == currency && d.DonationDate >= firstMonth && d.DonationDate <= now)
            .GroupBy(d => new { d.DonationDate.Year, d.DonationDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Amount = g.Sum(d => d.Amount) }).ToListAsync();
        var projectIds = await mine.Where(d => d.ReliefProjectId != null).Select(d => d.ReliefProjectId!.Value).Distinct().ToListAsync();
        var notifications = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);
        return new DonorOverviewViewModel
        {
            DisplayName = recent.FirstOrDefault()?.DonorName ?? accountName?.Split('@')[0] ?? "there",
            Currency = currency, Totals = totals, ContributionCount = totals.Sum(t => t.Count),
            SupportedProjectCount = projectIds.Count, RecentContributions = recent,
            UnreadNotificationCount = await notifications.CountAsync(n => n.ReadAtUtc == null),
            Notifications = await notifications.OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id).Take(3).ToListAsync(),
            SupportedProjects = await db.ReliefProjects.AsNoTracking().Where(p => projectIds.Contains(p.Id)).OrderByDescending(p => p.IsActive).ThenBy(p => p.Name).Take(3).ToListAsync(),
            Activity = Enumerable.Range(0, 6).Select(i =>
            {
                var month = firstMonth.AddMonths(i);
                return new MonthlyContribution { Month = month, Amount = activity.FirstOrDefault(a => a.Year == month.Year && a.Month == month.Month)?.Amount ?? 0m };
            }).ToList()
        };
    }
}
