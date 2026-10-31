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

    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<VolunteersController> _logger;

    public VolunteersController(
        IHttpClientFactory httpClientFactory,
        ApplicationDbContext db,
        ILogger<VolunteersController> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GiftOfTheGiversFunctions");
        _db = db;
        _logger = logger;
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
    // POST: Volunteers/Create (via Azure Function)
    // ============================================================
    [HttpPost]
    [ValidateAntiForgeryToken]
    [AllowAnonymous]
    public async Task<IActionResult> Create(Volunteer volunteer, string[]? selectedSkills)
    {
        if (selectedSkills is { Length: > 0 })
            volunteer.Skills = string.Join(", ", selectedSkills);

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

        int createdId = 0;

        // 1. Send to Azure Function: POST /api/volunteers (matches DJsController Create)
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/volunteers", volunteer);
            if (response.IsSuccessStatusCode)
            {
                var created = await response.Content.ReadFromJsonAsync<Volunteer>();
                if (created != null && created.Id > 0)
                {
                    createdId = created.Id;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Function offline — falling back to direct DbContext.");
        }

        // 2. Fallback to direct DB context if Azure Function was unreachable
        if (createdId == 0)
        {
            _db.Volunteers.Add(volunteer);
            await _db.SaveChangesAsync();
            createdId = volunteer.Id;
        }

        return RedirectToAction(nameof(ThankYou), new { id = createdId });
    }

    // ============================================================
    // GET: Volunteers/ThankYou/5 (via Azure Function)
    // ============================================================
    [AllowAnonymous]
    public async Task<IActionResult> ThankYou(int id)
    {
        Volunteer? volunteer = null;

        // Try getting volunteer from Azure Function
        try
        {
            var response = await _httpClient.GetAsync($"api/volunteers/{id}");
            if (response.IsSuccessStatusCode)
            {
                volunteer = await response.Content.ReadFromJsonAsync<Volunteer>();
            }
        }
        catch { }

        // Fallback to local DB
        volunteer ??= await _db.Volunteers.FindAsync(id);
        if (volunteer == null) return NotFound();

        return View(volunteer);
    }
}

