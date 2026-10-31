using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GiftOfTheGivers.Data;
using GiftOfTheGivers.Helpers;
using GiftOfTheGivers.Models;
using GiftOfTheGivers.Models.ViewModels;

namespace GiftOfTheGivers.Areas.Staff.Controllers;

/// <summary>
/// Staff-only dashboard area. Every action here requires the "Staff" role
/// ([Authorize(Roles = "Staff")]) — try opening /Staff/Dashboard while logged
/// out or as a Donor/Volunteer and you'll be redirected to Login / Access Denied.
/// </summary>
[Area("Staff")]
[Authorize(Roles = "Staff")]
public class StaffController : Controller
{
    private readonly ApplicationDbContext _db;

    public StaffController(ApplicationDbContext db) => _db = db;

    // ---- Overview ----------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        return View(new StaffDashboardViewModel
        {
            VolunteerCount = await _db.Volunteers.CountAsync(),
            DonationCount = await _db.Donations.CountAsync(),
            UpdateCount = await _db.ProjectUpdates.CountAsync(),
            ProjectCount = await _db.ReliefProjects.CountAsync(),
            // SQLite can't SUM() decimals server-side, so sum in memory
            TotalRaisedZar = (await _db.Donations
                .Where(d => d.Currency == "ZAR")
                .Select(d => d.Amount)
                .ToListAsync()).Sum(),
            RecentVolunteers = await _db.Volunteers
                .OrderByDescending(v => v.SignUpDate).Take(5).ToListAsync(),
            RecentDonations = await _db.Donations
                .OrderByDescending(d => d.DonationDate).Take(5).ToListAsync(),
            RecentUpdates = await _db.ProjectUpdates
                .Include(u => u.ReliefProject)
                .OrderByDescending(u => u.PostedAt).Take(3).ToListAsync(),
        });
    }

    // ---- Volunteer applications --------------------------------------------
    public async Task<IActionResult> Volunteers()
    {
        return View(await _db.Volunteers
            .OrderByDescending(v => v.SignUpDate)
            .ToListAsync());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var volunteer = await _db.Volunteers.FindAsync(id);
        if (volunteer == null) return NotFound();

        if (status is "Approved" or "Declined" or "Pending")
        {
            volunteer.Status = status;
            await _db.SaveChangesAsync();
            TempData["Flash"] = $"{volunteer.FullName}'s application is now {status}.";
        }

        return RedirectToAction(nameof(Volunteers));
    }

    // ---- Field updates ------------------------------------------------------
    public async Task<IActionResult> Updates()
    {
        return View(new StaffUpdatesViewModel
        {
            Projects = await _db.ReliefProjects.OrderBy(p => p.Name).ToListAsync(),
            RecentUpdates = await _db.ProjectUpdates
                .Include(u => u.ReliefProject)
                .OrderByDescending(u => u.PostedAt)
                .Take(10)
                .ToListAsync(),
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PostUpdate(ProjectUpdate update)
    {
        if (!ModelState.IsValid)
        {
            // Redisplay with validation messages
            return View("Updates", new StaffUpdatesViewModel
            {
                Projects = await _db.ReliefProjects.OrderBy(p => p.Name).ToListAsync(),
                RecentUpdates = await _db.ProjectUpdates
                    .Include(u => u.ReliefProject)
                    .OrderByDescending(u => u.PostedAt)
                    .Take(10)
                    .ToListAsync(),
                NewUpdate = update,
            });
        }

        update.PostedByName = User.Identity?.Name ?? "Staff";
        update.PostedAt = DateTime.Now;

        _db.ProjectUpdates.Add(update);
        await _db.SaveChangesAsync();

        TempData["Flash"] = "Update posted successfully.";
        return RedirectToAction(nameof(Updates));
    }

    // ---- Donations -----------------------------------------------------------
    public async Task<IActionResult> Donations()
    {
        var donations = await _db.Donations.OrderByDescending(d => d.DonationDate).ToListAsync();
        var zarAmounts = donations.Where(d => d.Currency == "ZAR").Select(d => d.Amount).ToList();

        // SECTION D.2 EVIDENCE: Using GiftOfTheGivers.Helpers to compute metrics
        var totalZar = DonationMetricsHelper.CalculateTotal(zarAmounts);
        var averageZar = DonationMetricsHelper.CalculateAverage(zarAmounts);

        ViewBag.TotalZar = totalZar;
        ViewBag.AverageZar = averageZar;
        ViewBag.FormattedTotal = DonationMetricsHelper.FormatZar(totalZar);
        ViewBag.FormattedAverage = DonationMetricsHelper.FormatZar(averageZar);
        ViewBag.DonationCount = donations.Count;
        return View(donations);
    }
}
