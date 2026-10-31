using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Helpers.Models.ViewModels;
namespace GiftOfTheGivers.Models;

public sealed class DonorOverviewViewModel
{
    public string DisplayName { get; set; } = "there";
    public string Currency { get; set; } = "ZAR";
    public List<CurrencySummary> Totals { get; set; } = new();
    public CurrencySummary SelectedTotal => Totals.FirstOrDefault(t => t.Currency == Currency) ?? new CurrencySummary { Currency = Currency };
    public int ContributionCount { get; set; }
    public int SupportedProjectCount { get; set; }
    public int UnreadNotificationCount { get; set; }
    public List<Donation> RecentContributions { get; set; } = new();
    public List<Notification> Notifications { get; set; } = new();
    public List<MonthlyContribution> Activity { get; set; } = new();
    public List<ReliefProject> SupportedProjects { get; set; } = new();
    public Donation? LatestContribution => RecentContributions.FirstOrDefault();
    public decimal LargestMonth => Math.Max(1m, Activity.Count == 0 ? 0m : Activity.Max(m => m.Amount));
}
public sealed class MonthlyContribution
{
    public DateTime Month { get; set; }
    public decimal Amount { get; set; }
}
