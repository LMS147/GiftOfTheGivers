using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Models.ViewModels;

namespace GiftOfTheGivers.Areas.Volunteer.Controllers;

/// <summary>
/// Volunteer dashboard area — only for users with the "Volunteer" role.
/// Shows the volunteer's own application status, active projects and updates.
/// </summary>
[Area("Volunteer")]
[Authorize(Roles = "Volunteer")]
public class VolunteerDashboardController : Controller
{
    private readonly ApplicationDbContext _db;

    public VolunteerDashboardController(ApplicationDbContext db) => _db = db;

    /// <summary>This volunteer's most recent application, if any.</summary>
    private async Task<Helpers.Models.Volunteer?> CurrentApplicationAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return await _db.Volunteers
            .Where(v => v.UserId == userId)
            .OrderByDescending(v => v.SignUpDate)
            .FirstOrDefaultAsync();
    }

    public async Task<IActionResult> Dashboard()
    {
        return View(await BuildViewModelAsync());
    }

    public async Task<IActionResult> MyApplication()
    {
        return View(await BuildViewModelAsync());
    }

    public async Task<IActionResult> Updates()
    {
        return View(await _db.ProjectUpdates
            .Include(u => u.ReliefProject)
            .OrderByDescending(u => u.PostedAt)
            .ToListAsync());
    }

    private async Task<VolunteerDashboardViewModel> BuildViewModelAsync()
    {
        return new VolunteerDashboardViewModel
        {
            Application = await CurrentApplicationAsync(),
            Projects = await _db.ReliefProjects.OrderBy(p => p.Name).ToListAsync(),
            LatestUpdates = await _db.ProjectUpdates
                .Include(u => u.ReliefProject)
                .OrderByDescending(u => u.PostedAt)
                .Take(5)
                .ToListAsync(),
        };
    }
}
