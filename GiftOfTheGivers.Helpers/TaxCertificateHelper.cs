using System.Security.Cryptography;
using System.Text;

namespace GiftOfTheGivers.Helpers;

/// <summary>
/// Section D.1 Utility Class: Provides formatting and calculation methods
/// for South African SARS Section 18A tax-deductible donation certificates.
/// </summary>
public static class TaxCertificateHelper
{
    public const string OfficialPboNumber = "930018993";
    public const string OfficialNpoNumber = "028-871-NPO";
    public const string OrganizationName = "Gift of the Givers Foundation";

    /// <summary>
    /// Generates a standardized Section 18A Tax Certificate Number.
    /// Example format: GOTG-18A-2026-00042
    /// </summary>
    /// <param name="donationId">Database unique donation identifier.</param>
    /// <param name="issueDate">Optional issue date (defaults to current UTC date).</param>
    /// <returns>Formatted certificate number string.</returns>
    public static string FormatCertificateNumber(int donationId, DateTime? issueDate = null)
    {
        var year = (issueDate ?? DateTime.UtcNow).Year;
        return $"GOTG-18A-{year}-{donationId:D5}";
    }

    /// <summary>
    /// Calculates the estimated SARS tax relief/rebate for an eligible Section 18A donation.
    /// Under South African tax law, bona fide donations to approved PBOs can reduce taxable
    /// income up to 10% of taxable income.
    /// </summary>
    /// <param name="donationAmount">Amount donated in ZAR.</param>
    /// <param name="marginalTaxRate">Estimated marginal tax rate (default 28% for corporate/mid-tier individual).</param>
    /// <returns>Estimated tax savings in ZAR.</returns>
    public static decimal CalculateTaxRelief(decimal donationAmount, decimal marginalTaxRate = 0.28m)
    {
        if (donationAmount <= 0) return 0m;
        if (marginalTaxRate is < 0m or > 1m) marginalTaxRate = 0.28m;
        return Math.Round(donationAmount * marginalTaxRate, 2);
    }

    /// <summary>
    /// Returns the official SARS Public Benefit Organisation (PBO) registration details.
    /// </summary>
    public static string GetPboRegistrationDetails()
    {
        return $"PBO Reg No: {OfficialPboNumber} | NPO Reg No: {OfficialNpoNumber}";
    }

    /// <summary>
    /// Returns the statutory Section 18A compliance notice required on certificates.
    /// </summary>
    public static string GetLegalComplianceNotice()
    {
        return "Student prototype. No payment has been processed. This is not a valid tax certificate.";
    }

    /// <summary>
    /// Generates a cryptographic verification token for the certificate to confirm authenticity.
    /// </summary>
    public static string GenerateVerificationHash(string certNumber, decimal amount, string donorEmail)
    {
        var rawData = $"{certNumber}:{amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)}:{donorEmail.ToLowerInvariant()}:{OfficialPboNumber}";
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToHexString(hashBytes)[..16];
    }
}

