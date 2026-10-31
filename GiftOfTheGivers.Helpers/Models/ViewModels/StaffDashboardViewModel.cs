namespace GiftOfTheGivers.Helpers.Models.ViewModels;

/// <summary>Data for the Staff dashboard overview page.</summary>
public class StaffDashboardViewModel
{
    public int PendingVolunteerCount { get; set; }
    public int ActiveProjectCount { get; set; }
    public int VolunteerCount { get; set; }
    public int DonationCount { get; set; }
    public int UpdateCount { get; set; }
    public int ProjectCount { get; set; }
    public decimal TotalRaisedZar { get; set; }

    public List<Volunteer> RecentVolunteers { get; set; } = new();
    public List<Donation> RecentDonations { get; set; } = new();
    public List<ProjectUpdate> RecentUpdates { get; set; } = new();
}
