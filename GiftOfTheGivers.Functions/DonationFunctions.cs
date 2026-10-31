using GiftOfTheGivers.Helpers;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Functions;

public class DonationFunctions
{
    // ============================================================
    // Declare the database context (housed in the Class Library).
    // This will be used by all the functions to access the Donations table.
    // ============================================================
    private readonly ApplicationDbContext _context;

    // Constructor
    // ApplicationDbContext is injected into this class using Dependency Injection.
    public DonationFunctions(ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // READ ALL DONATIONS
    // HTTP METHOD: GET
    // URL: /api/donations
    // CRUD OPERATION: READ
    // ============================================================

    [Function("GetDonations")]
    public async Task<HttpResponseData> GetDonations(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "donations")] HttpRequestData req)
    {
        // Retrieve all donations from the database (via Class Library model & DbContext).
        // ToListAsync() executes the query asynchronously.
        var donations = await _context.Donations
            .OrderByDescending(d => d.DonationDate)
            .ToListAsync();

        // Create an HTTP response with status code: 200 OK
        var response = req.CreateResponse(HttpStatusCode.OK);

        // Convert the list of donations to JSON and place it inside the HTTP response body.
        await response.WriteAsJsonAsync(donations);

        // Send the response back to the application that called this function.
        return response;
    }

    // ============================================================
    // READ ONE DONATION
    // HTTP METHOD: GET
    // Example URL: /api/donations/5
    // CRUD OPERATION: READ
    // ============================================================

    [Function("GetDonationById")]
    public async Task<HttpResponseData> GetDonationById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "donations/{id:int}")] HttpRequestData req, int id)
    {
        // Search the database for a donation whose primary key
        // matches the ID received from the URL.
        var donation = await _context.Donations.FindAsync(id);

        // If the donation cannot be found, return: 404 Not Found
        if (donation == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        // If the donation was found, create: 200 OK
        var response = req.CreateResponse(HttpStatusCode.OK);

        // Convert the donation object to JSON and place it inside the response body.
        await response.WriteAsJsonAsync(donation);

        // Return the donation to the calling application.
        return response;
    }

    // ============================================================
    // CREATE A NEW DONATION
    // HTTP METHOD: POST
    // URL: /api/donations
    // CRUD OPERATION: CREATE
    // ============================================================

    [Function("CreateDonation")]
    public async Task<HttpResponseData> CreateDonation(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "donations")] HttpRequestData req)
    {
        // Read the JSON data sent in the HTTP request body and convert it into a Donation object.
        var donation = await req.ReadFromJsonAsync<Donation>();

        // If the request did not contain valid donation data, return: 400 Bad Request
        if (donation == null || donation.Email.Length > 254 || !Validator.TryValidateObject(donation, new ValidationContext(donation), new List<ValidationResult>(), true))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (donation.DonationDate == default)
        {
            donation.DonationDate = DateTime.Now;
        }

        // Add the new donation to the database.
        if (donation.ReliefProjectId != null && !await _context.ReliefProjects.AnyAsync(p => p.Id == donation.ReliefProjectId && p.IsActive))
            return req.CreateResponse(HttpStatusCode.BadRequest);
        donation.ReliefProject = null;
        donation.Id = 0;
        donation.DonationDate = DateTime.UtcNow;
        _context.Donations.Add(donation);
        await _context.SaveChangesAsync();

        // Use the shared TaxCertificateHelper (from the Class Library)
        // to assign a formal SARS Section 18A tax certificate number.
        donation.ReferenceNumber = TaxCertificateHelper.FormatCertificateNumber(donation.Id, donation.DonationDate);
        _context.Notifications.Add(new Notification { UserId = donation.DonorId, Email = donation.Email, Subject = "Your demo donation was recorded", Body = "Your demo contribution has been recorded. No payment was processed. This is not a tax receipt." });
        await _context.SaveChangesAsync();

        // Create a response with: 201 Created
        var response = req.CreateResponse(HttpStatusCode.Created);

        // Return the newly created donation as JSON with generated ID and Reference.
        await response.WriteAsJsonAsync(donation);

        return response;
    }

    // ============================================================
    // GENERATE SECTION 18A TAX CERTIFICATE
    // HTTP METHOD: GET or POST
    // Example URLs:
    //   /api/donations/5/certificate
    //   /api/tax-certificate?donationId=5&donorName=Sarah&amount=1500
    // ============================================================

    [Function("GenerateTaxCertificateById")]
    public async Task<HttpResponseData> GenerateTaxCertificateById(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "donations/{id:int}/certificate")] HttpRequestData req, int id)
    {
        var donation = await _context.Donations.FindAsync(id);
        if (donation == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var certNumber = string.IsNullOrWhiteSpace(donation.ReferenceNumber)
            ? TaxCertificateHelper.FormatCertificateNumber(donation.Id, donation.DonationDate)
            : donation.ReferenceNumber;

        var taxRelief = TaxCertificateHelper.CalculateTaxRelief(donation.Amount);
        var formattedAmount = $"{donation.Currency} {donation.Amount:N2}";
        var tier = DonationMetricsHelper.GetDonationTier(donation.Amount);
        var verificationHash = TaxCertificateHelper.GenerateVerificationHash(certNumber, donation.Amount, donation.Email);

        var certResult = new
        {
            success = true,
            certificateNumber = certNumber,
            pboNumber = TaxCertificateHelper.OfficialPboNumber,
            npoNumber = TaxCertificateHelper.OfficialNpoNumber,
            organization = TaxCertificateHelper.OrganizationName,
            donationId = donation.Id,
            donorName = donation.DonorName,
            email = donation.Email,
            amount = donation.Amount,
            currency = donation.Currency ?? "ZAR",
            formattedAmount = formattedAmount,
            estimatedTaxRelief = $"R {taxRelief:N2}",
            estimatedTaxReliefValue = taxRelief,
            donorTier = tier,
            issueDate = donation.DonationDate,
            verificationHash = verificationHash,
            legalNotice = TaxCertificateHelper.GetLegalComplianceNotice(),
            generatedBy = "Azure Function: DonationFunctions.GenerateTaxCertificateById"
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(certResult);
        return response;
    }

    // ============================================================
    // DELETE A DONATION
    // HTTP METHOD: DELETE
    // Example URL: /api/donations/5
    // CRUD OPERATION: DELETE
    // ============================================================

    [Function("DeleteDonation")]
    public async Task<HttpResponseData> DeleteDonation(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "donations/{id:int}")] HttpRequestData req, int id)
    {
        // Find the donation that must be deleted using the ID received from the URL.
        var donation = await _context.Donations.FindAsync(id);

        // If the donation does not exist, return: 404 Not Found
        if (donation == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        // Tell Entity Framework to remove the donation from the database.
        _context.StaffAuditEntries.Add(new StaffAuditEntry { ActorName = "Trusted API", Action = "Deleted", EntityType = "Donation", EntityId = id, Summary = $"Deleted demo record {donation.ReferenceNumber}." });
        _context.Donations.Remove(donation);

        // Execute the DELETE operation against the database.
        await _context.SaveChangesAsync();

        // Return: 204 No Content
        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}

