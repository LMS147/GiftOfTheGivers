using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;

namespace GiftOfTheGivers.Controllers;

/// <summary>
/// Volunteer sign-up is open to everyone (guest-friendly). The resulting list
/// is only visible to Employees on the staff dashboard.
/// </summary>
public class VolunteersController : Controller
{
    /// <summary>Options for the "skills" checkboxes on the form.</summary>
    public static readonly string[] Skills =
    {
        "First Aid", "Logistics & Distribution", "Food Preparation", "Counselling",
        "Building & Repairs", "Driving", "Fundraising", "Communications", "Other"
    };

    public static readonly string[] AvailabilityOptions = { "Weekdays", "Weekends", "Full-time", "On-call" };

    private readonly SubmissionAccessService _access;
    private readonly ExperienceService _experience;
    private readonly ApplicationDbContext _db;

    public VolunteersController(
        SubmissionAccessService access,
        ApplicationDbContext db, ExperienceService experience)
    {
        _access = access;
        _db = db;
        _experience = experience;
    }

    // ============================================================
    // GET: Volunteers/Create
    // ============================================================
    [AllowAnonymous]
    public IActionResult Create()
    {
        ViewData["Skills"] = Skills;
        ViewData["Availability"] = AvailabilityOptions;
        return View(new Volunteer());
    }

    // ============================================================
    // POST: Volunteers/Create (database-backed)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Create([Bind("FullName,Email,Phone,Availability,Notes")] Volunteer volunteer, string[]? selectedSkills)
    {
        if (selectedSkills is { Length: > 0 })
            volunteer.Skills = string.Join(", ", selectedSkills);

        if (volunteer.Email.Length > 254) ModelState.AddModelError(nameof(volunteer.Email), "Email address must be at most 254 characters.");
        if (!ModelState.IsValid)
        {
            ViewData["Skills"] = Skills;
            ViewData["Availability"] = AvailabilityOptions;
            ViewData["SelectedSkills"] = selectedSkills ?? Array.Empty<string>();
            return View(volunteer);
        }

        volunteer.SignUpDate = DateTime.Now;
        volunteer.UserId = User.Identity?.IsAuthenticated == true
            ? User.FindFirstValue(ClaimTypes.NameIdentifier)
            : null;

        volunteer.Status = "Pending";
        _db.Volunteers.Add(volunteer);
        _experience.Notify(volunteer.UserId, volunteer.Email, "Volunteer application received",
            $"Hello {volunteer.FullName}. Your volunteer application is pending review. Our team will contact you after a decision. You do not need to apply again.");
        await _db.SaveChangesAsync();
        var createdId = volunteer.Id;
        _access.Grant(HttpContext, "volunteer", createdId);

        return RedirectToAction(nameof(ThankYou), new { id = createdId });
    }

    // ============================================================
    // GET: Volunteers/ThankYou/5 (database-backed)
    // ============================================================
    [AllowAnonymous]
    public async Task<IActionResult> ThankYou(int id)
    {
        var volunteer = await _db.Volunteers.FindAsync(id);
        if (volunteer == null || !_access.CanRead(HttpContext, "volunteer", id, volunteer.UserId)) return NotFound();
        Response.Headers.CacheControl = "no-store";

        return View(volunteer);
    }
}


