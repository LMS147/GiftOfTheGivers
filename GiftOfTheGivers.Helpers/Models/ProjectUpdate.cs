using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace GiftOfTheGivers.Helpers.Models;

/// <summary>
/// A news update that an Employee posts about a relief project. The latest
/// updates are shown on the home page so donors and volunteers see the same
/// live information.
/// </summary>
public class ProjectUpdate
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please choose a project.")]
    [Display(Name = "Relief project")]
    public int ReliefProjectId { get; set; }

    [ForeignKey(nameof(ReliefProjectId))]
    public ReliefProject? ReliefProject { get; set; }

    [Required(ErrorMessage = "Please give the update a short title.")]
    [StringLength(150)]
    [Display(Name = "Title")]
    public string Title { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please write the update.")]
    [StringLength(2000)]
    [Display(Name = "Update")]
    public string Body { get; set; } = string.Empty;

    [StringLength(100)]
    [Display(Name = "Posted by")]
    public string? PostedByName { get; set; }

    [Display(Name = "Posted at")]
    public DateTime PostedAt { get; set; } = DateTime.Now;
}
