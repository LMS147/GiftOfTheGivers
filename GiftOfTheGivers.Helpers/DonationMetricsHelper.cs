using System.Globalization;

namespace GiftOfTheGivers.Helpers;

/// <summary>
/// Section D.1 Utility Class: Provides aggregation, tier categorization,
/// and South African Rand (ZAR) currency formatting for donations.
/// </summary>
public static class DonationMetricsHelper
{
    private static readonly CultureInfo ZaCulture = new("en-ZA");

    /// <summary>
    /// Formats a monetary amount into standard South African Rand string (e.g., "R 1,500.00").
    /// </summary>
    public static string FormatZar(decimal amount)
    {
        return $"R {amount.ToString("N2", CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Calculates the total sum across a collection of donation amounts.
    /// </summary>
    public static decimal CalculateTotal(IEnumerable<decimal> amounts)
    {
        if (amounts == null) return 0m;
        return amounts.Sum();
    }

    /// <summary>
    /// Calculates the average donation amount across a collection.
    /// </summary>
    public static decimal CalculateAverage(IEnumerable<decimal> amounts)
    {
        if (amounts == null) return 0m;
        var list = amounts.ToList();
        return list.Count == 0 ? 0m : Math.Round(list.Average(), 2);
    }

    /// <summary>
    /// Generates a standardized donor transaction reference.
    /// Example: GOTG-REF-SMITH-00101
    /// </summary>
    public static string GenerateDonorReference(string? donorName, int donationId)
    {
        var cleanName = string.IsNullOrWhiteSpace(donorName)
            ? "ANON"
            : new string(donorName.Where(char.IsLetter).Take(5).ToArray()).ToUpperInvariant();

        return $"GOTG-REF-{cleanName}-{donationId:D5}";
    }

    /// <summary>
    /// Categorizes a donation amount into donor recognition tiers.
    /// </summary>
    public static string GetDonationTier(decimal amount) => amount switch
    {
        >= 10000m => "Platinum Partner",
        >= 2500m => "Gold Supporter",
        >= 500m => "Silver Friend",
        _ => "Community Contributor"
    };
}
