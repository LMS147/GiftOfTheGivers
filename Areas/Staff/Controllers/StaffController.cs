using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Helpers.Models.ViewModels;

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
    private readonly ExperienceService _experience;
    private readonly IConfiguration _config;
    private readonly HttpClient _functions;
    private readonly ILogger<StaffController> _logger;

    public StaffController(ApplicationDbContext db, IHttpClientFactory clients, ILogger<StaffController> logger, ExperienceService experience, IConfiguration config)
    {
        _db = db;
        _experience = experience;
        _config = config;
        _functions = clients.CreateClient("GiftOfTheGiversFunctions");
        _logger = logger;
    }

    // ---- Overview ----------------------------------------------------------
    public async Task<IActionResult> Dashboard()
    {
        return View(new StaffDashboardViewModel
        {
            PendingVolunteerCount = await _db.Volunteers.CountAsync(v => v.Status == "Pending"),
            ActiveProjectCount = await _db.ReliefProjects.CountAsync(p => p.IsActive),
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
    public async Task<IActionResult> Volunteers(string? q, string? status, int page = 1)
    {
        var query = _db.Volunteers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) { var term = q.Trim().ToLowerInvariant(); query = query.Where(v => v.FullName.ToLower().Contains(term) || v.Email.ToLower().Contains(term) || (v.Skills != null && v.Skills.ToLower().Contains(term))); }
        if (status is "Pending" or "Approved" or "Declined") query = query.Where(v => v.Status == status);
        return View(await ExperienceService.PageAsync(query.OrderByDescending(v => v.SignUpDate).ThenByDescending(v => v.Id), page));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, string status)
    {
        var volunteer = await _db.Volunteers.FindAsync(id);
        if (volunteer == null) return NotFound();

        if (status is not ("Approved" or "Declined" or "Pending")) return BadRequest();
        if (volunteer.Status == status) return RedirectToAction(nameof(Volunteers));
        var previous = volunteer.Status;
        volunteer.Status = status;
        var next = status == "Approved" ? "Your application has been approved. Our team will contact you about available opportunities."
            : status == "Declined" ? "Your application was declined. You can contact the support team to discuss future opportunities."
            : "Your application is awaiting review. No further action is needed right now.";
        _experience.Notify(volunteer.UserId, volunteer.Email, $"Volunteer application: {status}", $"Hello {volunteer.FullName}. {next}");
        _experience.Audit(User, "Status changed", "Volunteer", id, $"Application changed from {previous} to {status}.");
        await _db.SaveChangesAsync();
        TempData["Flash"] = $"{volunteer.FullName}'s application is now {status}.";

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
    public async Task<IActionResult> PostUpdate([Bind("ReliefProjectId,Title,Body", Prefix = "NewUpdate")] ProjectUpdate update)
    {
        if (!await _db.ReliefProjects.AnyAsync(p => p.Id == update.ReliefProjectId))
            ModelState.AddModelError("NewUpdate.ReliefProjectId", "Choose an existing project.");
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
        _experience.Audit(User, "Update posted", "Project", update.ReliefProjectId, $"Posted field update: {update.Title}");
        await _db.SaveChangesAsync();

        TempData["Flash"] = "Update posted successfully.";
        if (_functions.BaseAddress != null)
        {
            try
            {
                var response = await _functions.PostAsJsonAsync("api/log-update", new { updateId = update.Id });
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Update {Id} saved, but its cloud audit could not be stored", update.Id);
                TempData["Flash"] = "Update saved. Cloud audit storage is unavailable; the audit still needs to be retried.";
            }
        }
        return RedirectToAction(nameof(Updates));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryAudit(int id)
    {
        if (!await _db.ProjectUpdates.AnyAsync(u => u.Id == id)) return NotFound();
        if (_functions.BaseAddress == null)
        {
            TempData["Flash"] = "Configure Azure Functions before retrying cloud audit storage.";
            return RedirectToAction(nameof(Updates));
        }
        try
        {
            var response = await _functions.PostAsJsonAsync("api/log-update", new { updateId = id });
            response.EnsureSuccessStatusCode();
            TempData["Flash"] = "Cloud audit stored successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Audit retry failed for update {Id}", id);
            TempData["Flash"] = "Cloud audit storage is still unavailable. Try again after restoring the service.";
        }
        return RedirectToAction(nameof(Updates));
    }

    // ---- Donations -----------------------------------------------------------
    public async Task<IActionResult> Donations(string? q, string? currency, DateTime? from, DateTime? to, int page = 1)
    {
        var query = _db.Donations.AsNoTracking().Include(d => d.ReliefProject).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) { var term = q.Trim().ToLowerInvariant(); query = query.Where(d => d.DonorName.ToLower().Contains(term) || d.Email.ToLower().Contains(term) || d.ReferenceNumber.ToLower().Contains(term)); }
        if (currency is "ZAR" or "USD" or "EUR") query = query.Where(d => d.Currency == currency);
        if (from != null) query = query.Where(d => d.DonationDate >= from.Value.Date);
        if (to != null) { var end = to.Value.Date.AddDays(1); query = query.Where(d => d.DonationDate < end); }
        return View(await ExperienceService.PageAsync(query.OrderByDescending(d => d.DonationDate).ThenByDescending(d => d.Id), page));
    }
    public async Task<IActionResult> Audit(string? q, int page = 1)
    {
        var query = _db.StaffAuditEntries.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) { var term = q.Trim().ToLowerInvariant(); query = query.Where(a => a.ActorName.ToLower().Contains(term) || a.Action.ToLower().Contains(term) || a.Summary.ToLower().Contains(term)); }
        return View(await ExperienceService.PageAsync(query.OrderByDescending(a => a.OccurredAtUtc).ThenByDescending(a => a.Id), page, 20));
    }
    public async Task<IActionResult> Diagnostics() => View(new DiagnosticsViewModel
    {
        FunctionsConfigured = !string.IsNullOrWhiteSpace(_config["AzureFunctions:BaseUrl"]) && !string.IsNullOrWhiteSpace(_config["AzureFunctions:FunctionKey"]),
        EmailEnabled = _config.GetValue<bool>("Email:Enabled"),
        EmailConfigured = new EmailOptions { Host = _config["Email:Host"] ?? "", FromAddress = _config["Email:FromAddress"] ?? "" }.IsConfigured,
        PendingEmailCount = await _db.Notifications.CountAsync(n => n.SentAtUtc == null),
        FailedEmailCount = await _db.Notifications.CountAsync(n => n.SentAtUtc == null && n.DeliveryAttempts >= 5),
        RecentDeliveries = await _db.Notifications.OrderByDescending(n => n.CreatedAtUtc).Take(20).ToListAsync()
    });
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryEmail(int id)
    {
        var notification = await _db.Notifications.FindAsync(id);
        if (notification == null) return NotFound();
        if (notification.SentAtUtc != null || notification.DeliveryAttempts < 5) return BadRequest();
        notification.DeliveryAttempts = 0; notification.NextAttemptAtUtc = null; notification.LastDeliveryError = null;
        _experience.Audit(User, "Email retry", "Notification", id, "Queued a failed notification for delivery retry.");
        await _db.SaveChangesAsync();
        TempData["Flash"] = "Notification queued. Delivery requires enabled, configured SMTP.";
        return RedirectToAction(nameof(Diagnostics));
    }
}
