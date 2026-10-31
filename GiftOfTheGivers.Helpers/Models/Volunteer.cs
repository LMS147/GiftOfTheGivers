using System.ComponentModel.DataAnnotations;

namespace GiftOfTheGivers.Helpers.Models;

/// <summary>
/// A volunteer sign-up. Sign-ups are public (guests allowed) but the list is
/// only visible to Employees on the staff dashboard.
/// </summary>
public class Volunteer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Please enter your full name.")]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter a contact number.")]
    [StringLength(20)]
    [Display(Name = "Phone number")]
    public string Phone { get; set; } = string.Empty;

    /// <summary>Ticked checkboxes from the form, stored as one comma-separated
    /// string, e.g. "First Aid, Driving".</summary>
    [Display(Name = "Skills")]
    public string? Skills { get; set; }

    [Required(ErrorMessage = "Please choose your availability.")]
    [StringLength(20)]
    [Display(Name = "Availability")]
    public string Availability { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Anything else we should know?")]
    public string? Notes { get; set; }

    [Display(Name = "Sign-up date")]
    public DateTime SignUpDate { get; set; } = DateTime.Now;

    /// <summary>Set when a logged-in user signs up, otherwise null.</summary>
    public string? UserId { get; set; }

    /// <summary>Application status, managed by staff: Pending → Approved / Declined.</summary>
    [Display(Name = "Status")]
    public string Status { get; set; } = "Pending";
}
