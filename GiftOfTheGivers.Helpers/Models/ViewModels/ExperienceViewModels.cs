using System.ComponentModel.DataAnnotations;
namespace GiftOfTheGivers.Helpers.Models.ViewModels;

public class PaginationModel
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalCount { get; set; }
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}
public class PagedResult<T> : PaginationModel { public List<T> Items { get; set; } = new(); }
public class ProjectCard
{
    public ReliefProject Project { get; set; } = new();
    public decimal RecordedZar { get; set; }
    public int Progress => Project.TargetAmount <= 0 ? 0 : (int)Math.Min(100, RecordedZar / Project.TargetAmount * 100);
    public string Image => Project.ImageUrl ?? (Project.Category == "Water" ? "/images/project-water.svg" : Project.Category == "Shelter" ? "/images/project-shelter.svg" : "/images/project-food.svg");
}
public class ProjectDetailsViewModel
{
    public ProjectCard Card { get; set; } = new();
    public List<ProjectUpdate> Updates { get; set; } = new();
}
public class CurrencySummary
{
    public string Currency { get; set; } = "ZAR";
    public decimal Total { get; set; }
    public int Count { get; set; }
    public decimal Average => Count == 0 ? 0 : Total / Count;
}
public class DonorDashboardViewModel
{
    public PagedResult<Donation> Donations { get; set; } = new();
    public List<CurrencySummary> Totals { get; set; } = new();
    public int ContributionCount { get; set; }
    public int RecurringCount { get; set; }
}
public class DonationInput
{
    [Required, StringLength(100), Display(Name = "Full name")] public string DonorName { get; set; } = "";
    [Required, EmailAddress, StringLength(254), Display(Name = "Email address")] public string Email { get; set; } = "";
    [Range(typeof(decimal), "1", "1000000")] public decimal Amount { get; set; } = 250m;
    [Required, RegularExpression("^(ZAR|USD|EUR)$")] public string Currency { get; set; } = "ZAR";
    public bool IsRecurring { get; set; }
    [Display(Name = "Relief project")] public int? ReliefProjectId { get; set; }
    public Guid SubmissionKey { get; set; } = Guid.NewGuid();
}
public class DonationReviewViewModel
{
    public DonationInput Donation { get; set; } = new();
    public string ProjectName { get; set; } = "General relief fund";
    public string ReviewToken { get; set; } = "";
}
public class DiagnosticsViewModel
{
    public bool FunctionsConfigured { get; set; }
    public bool EmailEnabled { get; set; }
    public bool EmailConfigured { get; set; }
    public int PendingEmailCount { get; set; }
    public int FailedEmailCount { get; set; }
    public List<Notification> RecentDeliveries { get; set; } = new();
}
