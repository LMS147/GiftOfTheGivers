using System.Net.Http.Json;
using System.Text.Json;
using GiftOfTheGivers.Helpers;
using GiftOfTheGivers.Helpers.Models;

namespace GiftOfTheGivers.Services;

/// <summary>
/// Section A.1 Integration Service: Calls the Azure Function HTTP Trigger
/// when a donor completes a donation to generate an official tax certificate.
/// Uses GiftOfTheGivers.Helpers (Section D) for fallback and validation.
/// </summary>
public class TaxCertificateService : ITaxCertificateService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TaxCertificateService> _logger;

    public TaxCertificateService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<TaxCertificateService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        // Set reasonable timeout so cloud calls never block the donor UI indefinitely
        _httpClient.Timeout = TimeSpan.FromSeconds(5);
    }

    public string FunctionEndpointUrl =>
        _configuration["AzureFunctions:TaxCertificateUrl"]
        ?? "http://localhost:7071/api/tax-certificate";

    public async Task<TaxCertificateResult> GenerateTaxCertificateAsync(
        Donation donation,
        CancellationToken cancellationToken = default)
    {
        var endpoint = FunctionEndpointUrl;
        _logger.LogInformation("Invoking Azure Function at {Url} for donation #{Id}", endpoint, donation.Id);

        try
        {
            var payload = new
            {
                donationId = donation.Id,
                donorName = donation.DonorName,
                email = donation.Email,
                amount = donation.Amount,
                currency = donation.Currency ?? "ZAR",
                projectName = "Gift of the Givers Disaster Relief Fund"
            };

            var response = await _httpClient.PostAsJsonAsync(endpoint, payload, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var cloudResult = await response.Content.ReadFromJsonAsync<TaxCertificateResult>(options, cancellationToken);

                if (cloudResult != null)
                {
                    cloudResult.IsCloudGenerated = true;
                    cloudResult.StatusMessage = "Successfully generated and verified by Azure Function (HTTP Trigger)";
                    _logger.LogInformation("Azure Function returned certificate {CertNumber}", cloudResult.CertificateNumber);
                    return cloudResult;
                }
            }
            else
            {
                _logger.LogWarning("Azure Function returned HTTP {StatusCode}. Using local Helper fallback.", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Function at {Url} could not be reached. Using local Helper fallback.", endpoint);
        }

        // Graceful Fallback: Use GiftOfTheGivers.Helpers directly so the user is never stranded
        var issueDate = donation.DonationDate != default ? donation.DonationDate : DateTime.UtcNow;
        var certNumber = TaxCertificateHelper.FormatCertificateNumber(donation.Id, issueDate);
        var taxRelief = TaxCertificateHelper.CalculateTaxRelief(donation.Amount);
        var formattedAmount = DonationMetricsHelper.FormatZar(donation.Amount);
        var donorRef = DonationMetricsHelper.GenerateDonorReference(donation.DonorName, donation.Id);
        var tier = DonationMetricsHelper.GetDonationTier(donation.Amount);
        var hash = TaxCertificateHelper.GenerateVerificationHash(certNumber, donation.Amount, donation.Email);

        return new TaxCertificateResult
        {
            Success = true,
            CertificateNumber = certNumber,
            PboRegistrationNumber = TaxCertificateHelper.OfficialPboNumber,
            NpoRegistrationNumber = TaxCertificateHelper.OfficialNpoNumber,
            OrganizationName = TaxCertificateHelper.OrganizationName,
            DonationId = donation.Id,
            DonorName = donation.DonorName,
            Email = donation.Email,
            Amount = donation.Amount,
            Currency = donation.Currency ?? "ZAR",
            FormattedAmount = formattedAmount,
            EstimatedTaxRelief = $"R {taxRelief:N2}",
            EstimatedTaxReliefValue = taxRelief,
            DonorReference = donorRef,
            DonorTier = tier,
            ProjectName = "Disaster Relief General Fund",
            IssueDateUtc = issueDate,
            VerificationHash = hash,
            LegalNotice = TaxCertificateHelper.GetLegalComplianceNotice(),
            GeneratedBy = "Local Fallback (GiftOfTheGivers.Helpers)",
            IsCloudGenerated = false,
            StatusMessage = "Generated locally via GiftOfTheGivers.Helpers (Azure Function offline/unreachable)"
        };
    }
}
