namespace GiftOfTheGivers.Helpers.Models.ViewModels;

/// <summary>Data for the Volunteer dashboard pages.</summary>
public class VolunteerDashboardViewModel
{
    /// <summary>The volunteer's own application (null if they haven't signed up).</summary>
    public Volunteer? Application { get; set; }

    public List<ReliefProject> Projects { get; set; } = new();
    public List<ProjectUpdate> LatestUpdates { get; set; } = new();
}
