using GiftOfTheGivers.Helpers;
using GiftOfTheGivers.Helpers.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using System.Globalization;
using System.Text.Json;

namespace GiftOfTheGivers.Functions;

public class GenerateTaxCertificateFunction
{
    private readonly ApplicationDbContext _db;
    public GenerateTaxCertificateFunction(ApplicationDbContext db) => _db = db;

    [Function("GenerateTaxCertificate")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "tax-certificate")] HttpRequest req)
    {
        int id;
        if (HttpMethods.IsGet(req.Method))
        {
            if (!int.TryParse(req.Query["donationId"], out id)) return new BadRequestObjectResult("A donationId is required.");
        }
        else
        {
            try
            {
                var payload = await JsonSerializer.DeserializeAsync<CertificateRequest>(req.Body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                id = payload?.DonationId ?? 0;
            }
            catch (JsonException) { return new BadRequestObjectResult("Invalid JSON."); }
        }
        if (id <= 0) return new BadRequestObjectResult("A positive donationId is required.");
        var donation = await _db.Donations.FindAsync(id);
        if (donation == null) return new NotFoundResult();
        var number = TaxCertificateHelper.FormatCertificateNumber(donation.Id, donation.DonationDate);
        // Read saved values. Caller-supplied names and amounts cannot forge a receipt.
        return new OkObjectResult(new
        {
            success = true, certificateNumber = number, donationId = donation.Id,
            donorName = donation.DonorName, email = donation.Email,
            amount = donation.Amount, currency = donation.Currency,
            formattedAmount = donation.Currency + " " + donation.Amount.ToString("N2", CultureInfo.InvariantCulture),
            donorTier = DonationMetricsHelper.GetDonationTier(donation.Amount),
            issueDateUtc = donation.DonationDate,
            verificationHash = TaxCertificateHelper.GenerateVerificationHash(number, donation.Amount, donation.Email),
            legalNotice = TaxCertificateHelper.GetLegalComplianceNotice(),
            generatedBy = "Azure Functions (prototype receipt)"
        });
    }
    private sealed class CertificateRequest { public int DonationId { get; set; } }
}
