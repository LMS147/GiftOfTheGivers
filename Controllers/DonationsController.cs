using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers;
using GiftOfTheGivers.Helpers.Models;

namespace GiftOfTheGivers.Controllers;

/// <summary>
/// Donations controller: handles donation submissions, Section 18A tax certificate
/// generation via Azure Functions (Section A), and donor metrics calculation
/// via the GiftOfTheGivers.Helpers library (Section D).
/// </summary>
public class DonationsController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _db;
    private readonly ITaxCertificateService _taxCertService;
    private readonly ILogger<DonationsController> _logger;

    public DonationsController(
        IHttpClientFactory httpClientFactory,
        ApplicationDbContext db,
        ITaxCertificateService taxCertService,
        ILogger<DonationsController> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GiftOfTheGiversFunctions");
        _db = db;
        _taxCertService = taxCertService;
        _logger = logger;
    }

    // ============================================================
    // GET: Donations (Read all donations via Azure Function)
    // ============================================================
    [AllowAnonymous]
    public async Task<IActionResult> Index()
    {
        try
        {
            // Call Azure Function: GET /api/donations
            var donations = await _httpClient.GetFromJsonAsync<List<Donation>>("api/donations");
            if (donations != null) return View(donations);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Function offline — falling back to direct database context.");
        }

        // Direct DB fallback (from Class Library)
        var list = await _db.Donations.OrderByDescending(d => d.DonationDate).ToListAsync();
        return View(list);
    }

    // ============================================================
    // GET: Donations/Details/5 (Read one donation via Azure Function)
    // ============================================================
    [AllowAnonymous]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        try
        {
            var response = await _httpClient.GetAsync($"api/donations/{id}");
            if (response.IsSuccessStatusCode)
            {
                var donation = await response.Content.ReadFromJsonAsync<Donation>();
                return View(donation);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Function offline — falling back to direct database context.");
        }

        var localDonation = await _db.Donations.FindAsync(id);
        if (localDonation == null) return NotFound();
        return View(localDonation);
    }

    // ============================================================
    // GET: Donations/Create
    // ============================================================
    [AllowAnonymous]
    public IActionResult Create() => View(new Donation());

    // ============================================================
    // POST: Donations/Create (Create donation via Azure Function)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Create(Donation donation)
    {
        if (ModelState.IsValid)
        {
            donation.DonationDate = DateTime.Now;
            donation.DonorId = User.Identity?.IsAuthenticated == true
                ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;

            int createdId = 0;

            // 1. Send to Azure Function: POST /api/donations
            try
            {
                var response = await _httpClient.PostAsJsonAsync("api/donations", donation);
                if (response.IsSuccessStatusCode)
                {
                    var created = await response.Content.ReadFromJsonAsync<Donation>();
                    if (created != null && created.Id > 0)
                    {
                        createdId = created.Id;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Azure Function offline — saving via direct Class Library DbContext.");
            }

            // 2. Fallback to direct DB context if Azure Function was unreachable
            if (createdId == 0)
            {
                donation.ReferenceNumber = TaxCertificateHelper.FormatCertificateNumber(new Random().Next(100, 999), donation.DonationDate);
                _db.Donations.Add(donation);
                await _db.SaveChangesAsync();

                // Format final certificate number using Class Library helper
                donation.ReferenceNumber = TaxCertificateHelper.FormatCertificateNumber(donation.Id, donation.DonationDate);
                await _db.SaveChangesAsync();
                createdId = donation.Id;
            }

            return RedirectToAction(nameof(Certificate), new { id = createdId });
        }

        return View(donation);
    }

    // ============================================================
    // GET: Donations/Certificate/5 (Section 18A Tax Certificate)
    // ============================================================
    [AllowAnonymous]
    public async Task<IActionResult> Certificate(int id)
    {
        Donation? donation = null;

        // Try getting donation from Azure Function
        try
        {
            var response = await _httpClient.GetAsync($"api/donations/{id}");
            if (response.IsSuccessStatusCode)
            {
                donation = await response.Content.ReadFromJsonAsync<Donation>();
            }
        }
        catch { }

        // Fallback to local DB
        donation ??= await _db.Donations.FindAsync(id);
        if (donation == null) return NotFound();

        // Invoke tax certificate generation service
        var certResult = await _taxCertService.GenerateTaxCertificateAsync(donation);

        ViewBag.TaxCertificate = certResult;
        ViewBag.FormattedAmount = DonationMetricsHelper.FormatZar(donation.Amount);
        ViewBag.TaxRelief = DonationMetricsHelper.FormatZar(TaxCertificateHelper.CalculateTaxRelief(donation.Amount));
        ViewBag.DonorTier = DonationMetricsHelper.GetDonationTier(donation.Amount);
        ViewBag.PboDetails = TaxCertificateHelper.GetPboRegistrationDetails();

        return View(donation);
    }

    // ============================================================
    // GET: Donations/Delete/5
    // ============================================================
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> Delete(int? id)
    {
        if (id == null) return NotFound();
        var donation = await _db.Donations.FindAsync(id);
        if (donation == null) return NotFound();
        return View(donation);
    }

    // ============================================================
    // POST: Donations/Delete/5 (Delete via Azure Function)
    // ============================================================
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        try
        {
            await _httpClient.DeleteAsync($"api/donations/{id}");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Function delete failed — deleting directly from DbContext.");
            var donation = await _db.Donations.FindAsync(id);
            if (donation != null)
            {
                _db.Donations.Remove(donation);
                await _db.SaveChangesAsync();
            }
        }

        return RedirectToAction(nameof(Index));
    }

    // ============================================================
    // GET: MyDonations (Donor giving history with Class Library metrics)
    // ============================================================
    [Authorize]
    public async Task<IActionResult> MyDonations()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var donations = await _db.Donations
            .Where(d => d.DonorId == userId)
            .OrderByDescending(d => d.DonationDate)
            .ToListAsync();

        var amounts = donations.Select(d => d.Amount).ToList();
        var total = DonationMetricsHelper.CalculateTotal(amounts);
        var average = DonationMetricsHelper.CalculateAverage(amounts);
        var taxRelief = TaxCertificateHelper.CalculateTaxRelief(total);
        var tier = DonationMetricsHelper.GetDonationTier(total);

        ViewBag.TotalZar = DonationMetricsHelper.FormatZar(total);
        ViewBag.AverageZar = DonationMetricsHelper.FormatZar(average);
        ViewBag.TaxReliefTotal = DonationMetricsHelper.FormatZar(taxRelief);
        ViewBag.DonorTier = tier;

        return View(donations);
    }
}
