using GiftOfTheGivers.Helpers.Models;

namespace GiftOfTheGivers.Services;

/// <summary>
/// Service interface for communicating with the Azure Functions HTTP Trigger
/// to generate and verify dummy SARS Section 18A Tax Certificates.
/// </summary>
public interface ITaxCertificateService
{
    /// <summary>
    /// Invokes the Azure Function to generate official tax certificate metadata.
    /// Falls back gracefully to local helper generation if the function is offline.
    /// </summary>
    Task<TaxCertificateResult> GenerateTaxCertificateAsync(Donation donation, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the configured URL of the Azure Function endpoint.
    /// </summary>
    string FunctionEndpointUrl { get; }
}

public class TaxCertificateResult
{
    public bool Success { get; set; }
    public string CertificateNumber { get; set; } = string.Empty;
    public string PboRegistrationNumber { get; set; } = string.Empty;
    public string NpoRegistrationNumber { get; set; } = string.Empty;
    public string OrganizationName { get; set; } = string.Empty;
    public int DonationId { get; set; }
    public string DonorName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "ZAR";
    public string FormattedAmount { get; set; } = string.Empty;
    public string EstimatedTaxRelief { get; set; } = string.Empty;
    public decimal EstimatedTaxReliefValue { get; set; }
    public string DonorReference { get; set; } = string.Empty;
    public string DonorTier { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public DateTime IssueDateUtc { get; set; }
    public string VerificationHash { get; set; } = string.Empty;
    public string LegalNotice { get; set; } = string.Empty;
    public string GeneratedBy { get; set; } = string.Empty;
    public bool IsCloudGenerated { get; set; }
    public string StatusMessage { get; set; } = string.Empty;
}
