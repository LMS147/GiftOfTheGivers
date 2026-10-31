using System.ComponentModel.DataAnnotations;

namespace GiftOfTheGivers.Helpers.Models;

/// <summary>
/// A donation record. Anyone can donate — a logged-in donor (whose user id is
/// stored in DonorId) or an anonymous guest (DonorId stays null).
/// </summary>
public class Donation
{
    public int Id { get; set; }
    public int? ReliefProjectId { get; set; }
    public ReliefProject? ReliefProject { get; set; }
    public Guid? SubmissionKey { get; set; }

    [Required(ErrorMessage = "Please enter your name.")]
    [StringLength(100)]
    [Display(Name = "Full name")]
    public string DonorName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter your email address.")]
    [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
    [Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Please enter an amount.")]
    [Range(1, 1_000_000, ErrorMessage = "Amount must be at least 1.")]
    [DataType(DataType.Currency)]
    [Display(Name = "Amount")]
    public decimal Amount { get; set; }

    [Required(ErrorMessage = "Please choose a currency.")]
    [StringLength(3)]
    [RegularExpression("^(ZAR|USD|EUR)$", ErrorMessage = "Choose ZAR, USD or EUR.")]
    [Display(Name = "Currency")]
    public string Currency { get; set; } = "ZAR";

    /// <summary>True = monthly recurring donation. In this prototype it only
    /// stores the flag — there is no real payment gateway.</summary>
    [Display(Name = "Recurring donation")]
    public bool IsRecurring { get; set; }

    [Display(Name = "Donation date")]
    public DateTime DonationDate { get; set; } = DateTime.Now;

    /// <summary>Set when the donor donates while logged in (IdentityUser.Id),
    /// otherwise null for anonymous guests.</summary>
    public string? DonorId { get; set; }

    /// <summary>Unique reference number printed on the tax certificate.</summary>
    [Display(Name = "Reference number")]
    public string ReferenceNumber { get; set; } = string.Empty;
}

