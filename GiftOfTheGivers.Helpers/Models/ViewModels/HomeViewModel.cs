namespace GiftOfTheGivers.Helpers.Models.ViewModels;

/// <summary>Data shown on the public home page.</summary>
public class HomeViewModel
{
    public List<ProjectUpdate> LatestUpdates { get; set; } = new();
    public int DonationCount { get; set; }
    public decimal TotalDonatedZar { get; set; }
    public int VolunteerCount { get; set; }
    public int ProjectCount { get; set; }
}
