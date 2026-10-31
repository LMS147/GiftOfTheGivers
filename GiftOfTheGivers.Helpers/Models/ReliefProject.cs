using System.ComponentModel.DataAnnotations;

namespace GiftOfTheGivers.Helpers.Models;

/// <summary>
/// A relief operation (e.g. "KZN Flood Relief"). Employees post updates
/// against these projects on the staff dashboard.
/// </summary>
public class ReliefProject
{
    public int Id { get; set; }

    [Required]
    [StringLength(120)]
    [Display(Name = "Project name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(120)]
    [Display(Name = "Location")]
    public string? Location { get; set; }

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Required, StringLength(30)]
    [RegularExpression("^(Food|Water|Medical|Shelter|Education|Other)$")]
    public string Category { get; set; } = "Other";

    [Range(0, 100000000), Display(Name = "Demo target (ZAR)")]
    public decimal TargetAmount { get; set; }
    public bool IsActive { get; set; } = true;
    [StringLength(500), Display(Name = "Image URL (optional, HTTPS)")]
    public string? ImageUrl { get; set; }

    public List<ProjectUpdate> Updates { get; set; } = new();
}
