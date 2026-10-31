namespace GiftOfTheGivers.Helpers.Models.ViewModels;

/// <summary>Data for the Staff "Post update" page.</summary>
public class StaffUpdatesViewModel
{
    public List<ReliefProject> Projects { get; set; } = new();
    public List<ProjectUpdate> RecentUpdates { get; set; } = new();
    public ProjectUpdate NewUpdate { get; set; } = new();
}
