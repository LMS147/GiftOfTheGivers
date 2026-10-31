using System.Text.Json;
using System.Text.Json.Serialization;
using GiftOfTheGivers.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GiftOfTheGivers.Functions;

/// <summary>
/// Section A.1: HTTP-triggered Azure Function that generates a dummy
/// SARS Section 18A Tax Certificate when a donor completes a donation.
/// </summary>
public class GenerateTaxCertificateFunction
{
    private readonly ILogger<GenerateTaxCertificateFunction> _logger;

    public GenerateTaxCertificateFunction(ILogger<GenerateTaxCertificateFunction> logger)
    {
        _logger = logger;
    }

    [Function("GenerateTaxCertificate")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "tax-certificate")] HttpRequest req)
    {
        _logger.LogInformation("Processing tax certificate request via HTTP {Method}", req.Method);

        int donationId = 0;
        string? donorName = null;
        string? email = null;
        decimal amount = 0m;
        string currency = "ZAR";
        string? projectName = "Disaster Relief General Fund";

        // Handle GET: query string parameters (ideal for browser testing)
        if (HttpMethods.IsGet(req.Method))
        {
            if (int.TryParse(req.Query["donationId"], out var qId)) donationId = qId;
            donorName = req.Query["donorName"];
            email = req.Query["email"];
            if (decimal.TryParse(req.Query["amount"], out var qAmt)) amount = qAmt;
            if (!string.IsNullOrEmpty(req.Query["currency"])) currency = req.Query["currency"]!;
            if (!string.IsNullOrEmpty(req.Query["projectName"])) projectName = req.Query["projectName"]!;
        }
        // Handle POST: JSON request body (ideal for Web App integration and Postman)
        else if (HttpMethods.IsPost(req.Method))
        {
            try
            {
                using var reader = new StreamReader(req.Body);
                var requestBody = await reader.ReadToEndAsync();
                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var payload = JsonSerializer.Deserialize<TaxCertificateRequestDto>(requestBody, options);
                    if (payload != null)
                    {
                        donationId = payload.DonationId;
                        donorName = payload.DonorName;
                        email = payload.Email;
                        amount = payload.Amount;
                        currency = string.IsNullOrWhiteSpace(payload.Currency) ? "ZAR" : payload.Currency;
                        projectName = string.IsNullOrWhiteSpace(payload.ProjectName) ? projectName : payload.ProjectName;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to parse JSON body");
                return new BadRequestObjectResult(new
                {
                    success = false,
                    error = "Invalid JSON payload in request body: " + ex.Message
                });
            }
        }

        // Apply defaults if values not provided (convenient for quick manual tests)
        if (donationId <= 0) donationId = new Random().Next(100, 9999);
        if (string.IsNullOrWhiteSpace(donorName)) donorName = "Generous Donor";
        if (string.IsNullOrWhiteSpace(email)) email = "donor@example.com";
        if (amount <= 0m) amount = 500.00m;

        // Use custom Helper Library (Section D) for calculations and formatting
        var issueDate = DateTime.UtcNow;
        var certNumber = TaxCertificateHelper.FormatCertificateNumber(donationId, issueDate);
        var formattedAmount = DonationMetricsHelper.FormatZar(amount);
        var taxRelief = TaxCertificateHelper.CalculateTaxRelief(amount);
        var donorRef = DonationMetricsHelper.GenerateDonorReference(donorName, donationId);
        var tier = DonationMetricsHelper.GetDonationTier(amount);
        var verificationHash = TaxCertificateHelper.GenerateVerificationHash(certNumber, amount, email);

        _logger.LogInformation("Generated Tax Certificate {CertNumber} for {DonorName} ({Amount:C})",
            certNumber, donorName, amount);

        // Check if caller explicitly requested HTML rendering in the browser
        if (req.Query["format"] == "html" || req.Headers.Accept.ToString().Contains("text/html"))
        {
            var html = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8' />
    <title>Section 18A Certificate - {certNumber}</title>
    <style>
        body {{ font-family: 'Segoe UI', Arial, sans-serif; background: #f0fdf4; margin: 0; padding: 40px; color: #1e293b; }}
        .card {{ max-width: 680px; margin: 0 auto; background: #ffffff; border: 2px solid #0f766e; border-radius: 12px; padding: 36px; box-shadow: 0 10px 25px rgba(0,0,0,0.08); }}
        .header {{ text-align: center; border-bottom: 2px solid #e2e8f0; padding-bottom: 20px; }}
        .header h1 {{ color: #0f766e; margin: 0; font-size: 24px; }}
        .badge {{ display: inline-block; background: #134e4a; color: white; padding: 4px 12px; border-radius: 999px; font-size: 11px; margin-top: 8px; font-weight: 600; text-transform: uppercase; }}
        .details {{ margin: 24px 0; }}
        .row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px dashed #cbd5e1; }}
        .label {{ font-weight: 600; color: #64748b; }}
        .amount-box {{ background: #f0fdf4; border: 1px solid #86efac; border-radius: 8px; text-align: center; padding: 16px; margin: 20px 0; }}
        .amount {{ font-size: 32px; font-weight: bold; color: #0f766e; }}
        .footer {{ font-size: 11px; color: #64748b; text-align: center; margin-top: 24px; border-top: 1px solid #e2e8f0; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>
            <h1>Gift of the Givers Foundation</h1>
            <div class='badge'>Official Section 18A Tax Certificate (Azure Function)</div>
        </div>
        <div class='amount-box'>
            <div style='font-size: 12px; color: #0f766e; text-transform: uppercase;'>Total Contribution</div>
            <div class='amount'>{formattedAmount}</div>
            <div style='font-size: 13px; color: #166534;'>Estimated SARS Tax Relief: R {taxRelief:N2} (at 28%)</div>
        </div>
        <div class='details'>
            <div class='row'><span class='label'>Certificate Number:</span><span><strong>{certNumber}</strong></span></div>
            <div class='row'><span class='label'>Donor Name:</span><span>{donorName}</span></div>
            <div class='row'><span class='label'>Donor Email:</span><span>{email}</span></div>
            <div class='row'><span class='label'>Transaction Ref:</span><span>{donorRef}</span></div>
            <div class='row'><span class='label'>Project:</span><span>{projectName}</span></div>
            <div class='row'><span class='label'>Donor Tier:</span><span>{tier}</span></div>
            <div class='row'><span class='label'>PBO Registration:</span><span>{TaxCertificateHelper.OfficialPboNumber}</span></div>
            <div class='row'><span class='label'>Issue Date:</span><span>{issueDate:yyyy-MM-dd HH:mm:ss} UTC</span></div>
            <div class='row'><span class='label'>Verification Token:</span><code>{verificationHash}</code></div>
        </div>
        <div class='footer'>
            <p>{TaxCertificateHelper.GetLegalComplianceNotice()}</p>
            <p>Generated in real-time by Azure Functions HTTP Trigger (GiftOfTheGivers.Functions)</p>
        </div>
    </div>
</body>
</html>";
            return new ContentResult
            {
                Content = html,
                ContentType = "text/html",
                StatusCode = 200
            };
        }

        // Return structured JSON response for API / Web App / Postman
        var response = new TaxCertificateResponseDto
        {
            Success = true,
            CertificateNumber = certNumber,
            PboRegistrationNumber = TaxCertificateHelper.OfficialPboNumber,
            NpoRegistrationNumber = TaxCertificateHelper.OfficialNpoNumber,
            OrganizationName = TaxCertificateHelper.OrganizationName,
            DonationId = donationId,
            DonorName = donorName,
            Email = email,
            Amount = amount,
            Currency = currency,
            FormattedAmount = formattedAmount,
            EstimatedTaxRelief = $"R {taxRelief:N2}",
            EstimatedTaxReliefValue = taxRelief,
            DonorReference = donorRef,
            DonorTier = tier,
            ProjectName = projectName,
            IssueDateUtc = issueDate,
            VerificationHash = verificationHash,
            LegalNotice = TaxCertificateHelper.GetLegalComplianceNotice(),
            GeneratedBy = "Azure Functions (HTTP Trigger: GenerateTaxCertificate)",
            Timestamp = DateTime.UtcNow
        };

        return new OkObjectResult(response);
    }
}

public class TaxCertificateRequestDto
{
    [JsonPropertyName("donationId")]
    public int DonationId { get; set; }

    [JsonPropertyName("donorName")]
    public string? DonorName { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    [JsonPropertyName("currency")]
    public string? Currency { get; set; } = "ZAR";

    [JsonPropertyName("projectName")]
    public string? ProjectName { get; set; }
}

public class TaxCertificateResponseDto
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
    public DateTime Timestamp { get; set; }
}
