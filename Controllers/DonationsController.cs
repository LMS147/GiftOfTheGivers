using System.Security.Claims;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Helpers.Models.ViewModels;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace GiftOfTheGivers.Controllers;

public class DonationsController(ApplicationDbContext db, SubmissionAccessService access, DonationReviewService reviews, ExperienceService experience, ITaxCertificateService certificates) : Controller
{
    private string Owner => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "guest";
    [Authorize(Roles = "Staff")]
    public IActionResult Index() => RedirectToAction("Donations", "Staff", new { area = "Staff" });

    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> Details(int? id)
    {
        var item = await db.Donations.Include(d => d.ReliefProject).FirstOrDefaultAsync(d => d.Id == id);
        return item == null ? NotFound() : View(item);
    }
    [AllowAnonymous]
    public async Task<IActionResult> Create(int? projectId)
    {
        await ProjectChoicesAsync();
        if (projectId != null && !await db.ReliefProjects.AnyAsync(p => p.Id == projectId && p.IsActive))
            return NotFound();
        return View(new DonationInput { ReliefProjectId = projectId });
    }
    private async Task ProjectChoicesAsync() => ViewBag.Projects = await db.ReliefProjects.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> Review(DonationInput input)
    {
        if (input.SubmissionKey == Guid.Empty) ModelState.AddModelError("", "Please reload the donation form.");
        var project = input.ReliefProjectId == null ? null : await db.ReliefProjects.FindAsync(input.ReliefProjectId);
        if (input.ReliefProjectId != null && (project == null || !project.IsActive))
            ModelState.AddModelError(nameof(input.ReliefProjectId), "Choose an active project.");
        if (!ModelState.IsValid) { await ProjectChoicesAsync(); return View("Create", input); }
        return View(new DonationReviewViewModel { Donation = input, ProjectName = project?.Name ?? "General relief fund", ReviewToken = reviews.Protect(input, Owner) });
    }
    [HttpPost, ValidateAntiForgeryToken, AllowAnonymous]
    public async Task<IActionResult> Create(string reviewToken)
    {
        var input = reviews.Read(reviewToken, Owner);
        if (input == null)
        {
            TempData["Error"] = "Your review expired or changed. Please enter your donation details again.";
            return RedirectToAction(nameof(Create));
        }
        var existing = await db.Donations.FirstOrDefaultAsync(d => d.SubmissionKey == input.SubmissionKey);
        if (existing != null) return Confirmation(existing);
        if (input.ReliefProjectId != null && !await db.ReliefProjects.AnyAsync(p => p.Id == input.ReliefProjectId && p.IsActive))
        {
            TempData["Error"] = "That project has closed. Please choose another project.";
            return RedirectToAction(nameof(Create));
        }
        var donation = new Donation
        {
            DonorName = input.DonorName, Email = input.Email, Amount = input.Amount, Currency = input.Currency,
            IsRecurring = input.IsRecurring, ReliefProjectId = input.ReliefProjectId, SubmissionKey = input.SubmissionKey,
            DonationDate = DateTime.UtcNow, DonorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
        };
        db.Donations.Add(donation);
        experience.Notify(donation.DonorId, donation.Email, "Your demo donation was recorded",
            $"Thank you, {donation.DonorName}. Your demo record for {donation.Currency} {donation.Amount:N2} has been saved. No payment was processed. This is not a tax receipt.");
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            // A unique submission key makes a replay of the same reviewed form safe.
            db.ChangeTracker.Clear();
            existing = await db.Donations.FirstOrDefaultAsync(d => d.SubmissionKey == input.SubmissionKey);
            if (existing == null) throw;
            return Confirmation(existing);
        }
        donation.ReferenceNumber = TaxCertificateHelper.FormatCertificateNumber(donation.Id, donation.DonationDate);
        await db.SaveChangesAsync();
        return Confirmation(donation);
    }
    private IActionResult Confirmation(Donation donation)
    {
        if ((donation.DonorId ?? "guest") != Owner) return NotFound();
        access.Grant(HttpContext, "donation", donation.Id);
        return RedirectToAction(nameof(Certificate), new { id = donation.Id });
    }
    private Task<Donation?> ReadableAsync(int id) => db.Donations.Include(d => d.ReliefProject).FirstOrDefaultAsync(d => d.Id == id);

    [AllowAnonymous]
    public async Task<IActionResult> Certificate(int id)
    {
        var item = await ReadableAsync(id);
        if (item == null || !access.CanRead(HttpContext, "donation", id, item.DonorId)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        ViewBag.TaxCertificate = await certificates.GenerateTaxCertificateAsync(item);
        return View(item);
    }
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> CloudCertificate(int id)
    {
        var item = await ReadableAsync(id);
        if (item == null || !access.CanRead(HttpContext, "donation", id, item.DonorId)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return Json(await certificates.GenerateTaxCertificateAsync(item));
    }
    [HttpGet, AllowAnonymous]
    public async Task<IActionResult> Download(int id)
    {
        var item = await ReadableAsync(id);
        if (item == null || !access.CanRead(HttpContext, "donation", id, item.DonorId)) return NotFound();
        Response.Headers.CacheControl = "no-store";
        return File(ReceiptPdfService.Generate(item), "application/pdf", $"demo-receipt-{id}.pdf");
    }
    [Authorize(Roles = "Staff")]
    public async Task<IActionResult> Delete(int? id)
    {
        var item = await db.Donations.FindAsync(id);
        return item == null ? NotFound() : View(item);
    }
    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken, Authorize(Roles = "Staff")]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var item = await db.Donations.FindAsync(id);
        if (item == null) return NotFound();
        experience.Audit(User, "Deleted", "Donation", id, $"Deleted demo record {item.ReferenceNumber}.");
        db.Donations.Remove(item); await db.SaveChangesAsync();
        TempData["Flash"] = "Demo record deleted.";
        return RedirectToAction(nameof(Index));
    }
    [Authorize]
    public async Task<IActionResult> MyDonations(int page = 1, string? currency = null)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var mine = db.Donations.AsNoTracking().Where(d => d.DonorId == id);
        var summary = await mine.Select(d => new { d.Currency, d.Amount, d.IsRecurring }).ToListAsync();
        var filtered = mine.Include(d => d.ReliefProject).AsQueryable();
        if (!string.IsNullOrWhiteSpace(currency)) filtered = filtered.Where(d => d.Currency == currency);
        return View(new DonorDashboardViewModel
        {
            Donations = await ExperienceService.PageAsync(filtered.OrderByDescending(d => d.DonationDate).ThenByDescending(d => d.Id), page),
            Totals = summary.GroupBy(d => d.Currency).Select(g => new CurrencySummary { Currency = g.Key, Total = g.Sum(d => d.Amount), Count = g.Count() }).ToList(),
            ContributionCount = summary.Count, RecurringCount = summary.Count(d => d.IsRecurring)
        });
    }
}
