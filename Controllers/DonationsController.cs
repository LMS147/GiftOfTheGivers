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
    private readonly SubmissionAccessService _access;
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _db;
    private readonly ITaxCertificateService _taxCertService;
    private readonly ILogger<DonationsController> _logger;

    public DonationsController(
        IHttpClientFactory httpClientFactory,
        SubmissionAccessService access,
        ApplicationDbContext db,
        ITaxCertificateService taxCertService,
        ILogger<DonationsController> logger)
    {
        _access = access;
        _httpClient = httpClientFactory.CreateClient("GiftOfTheGiversFunctions");
        _db = db;
        _taxCertService = taxCertService;
        _logger = logger;
    }

    // ============================================================
    // GET: Donations (Read all donations via Azure Function)
    // ============================================================
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> Index()
    {
        if (_httpClient.BaseAddress != null) try
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
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> Details(int? id)
    {
        if (id == null) return NotFound();

        if (_httpClient.BaseAddress != null) try
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
    // POST: Donations/Create (single database write path)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Create([Bind("DonorName,Email,Amount,Currency,IsRecurring")] Donation donation)
    {
        if (ModelState.IsValid)
        {
            donation.DonationDate = DateTime.Now;
            donation.DonorId = User.Identity?.IsAuthenticated == true
                ? User.FindFirstValue(ClaimTypes.NameIdentifier)
                : null;

            _db.Donations.Add(donation);
            await _db.SaveChangesAsync();
            donation.ReferenceNumber = TaxCertificateHelper.FormatCertificateNumber(donation.Id, donation.DonationDate);
            await _db.SaveChangesAsync();
            var createdId = donation.Id;
            _access.Grant(HttpContext, "donation", createdId);

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
        var donation = await _db.Donations.FindAsync(id);
        if (donation == null || !_access.CanRead(HttpContext, "donation", id, donation.DonorId)) return NotFound();
        Response.Headers.CacheControl = "no-store";

        // Invoke tax certificate generation service
        var certResult = await _taxCertService.GenerateTaxCertificateAsync(donation);

        ViewBag.TaxCertificate = certResult;
        ViewBag.FormattedAmount = $"{donation.Currency} {donation.Amount:N2}";
        ViewBag.TaxRelief = DonationMetricsHelper.FormatZar(TaxCertificateHelper.CalculateTaxRelief(donation.Amount));
        ViewBag.DonorTier = DonationMetricsHelper.GetDonationTier(donation.Amount);
        ViewBag.PboDetails = TaxCertificateHelper.GetPboRegistrationDetails();

        return View(donation);
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> CloudCertificate(int id)
    {
        var donation = await _db.Donations.FindAsync(id);
        if (donation == null || !_access.CanRead(HttpContext, "donation", id, donation.DonorId)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Json(await _taxCertService.GenerateTaxCertificateAsync(donation));
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
    // POST: Donations/Delete/5
    // ============================================================
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var donation = await _db.Donations.FindAsync(id);
        if (donation == null) return NotFound();
        _db.Donations.Remove(donation);
        await _db.SaveChangesAsync();

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

        var amounts = donations.Where(d => d.Currency == "ZAR").Select(d => d.Amount).ToList();
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

