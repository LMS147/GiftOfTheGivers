using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace GiftOfTheGivers.Areas.Staff.Controllers;

[Area("Staff"), Authorize(Roles = "Staff")]
public class ProjectsController(ApplicationDbContext db, ExperienceService experience) : Controller
{
    public async Task<IActionResult> Index(string? q, string? status, int page = 1)
    {
        var query = db.ReliefProjects.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) { var term = q.Trim().ToLowerInvariant(); query = query.Where(p => p.Name.ToLower().Contains(term) || (p.Location != null && p.Location.ToLower().Contains(term))); }
        if (status == "active") query = query.Where(p => p.IsActive);
        if (status == "closed") query = query.Where(p => !p.IsActive);
        return View(await ExperienceService.PageAsync(query.OrderByDescending(p => p.IsActive).ThenBy(p => p.Name), page));
    }
    [HttpGet] public IActionResult Create() => View("Edit", new ReliefProject());
    [HttpGet] public async Task<IActionResult> Edit(int id)
    {
        var item = await db.ReliefProjects.FindAsync(id);
        return item == null ? NotFound() : View(item);
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(int id, [Bind("Name,Location,Description,Category,TargetAmount,IsActive,ImageUrl")] ReliefProject input)
    {
        if (!string.IsNullOrWhiteSpace(input.ImageUrl) && (!Uri.TryCreate(input.ImageUrl, UriKind.Absolute, out var image) || image.Scheme != "https"))
            ModelState.AddModelError(nameof(input.ImageUrl), "Use an HTTPS image URL, or leave blank for an illustration.");
        if (string.IsNullOrWhiteSpace(input.ImageUrl)) input.ImageUrl = null;
        if (!ModelState.IsValid) { input.Id = id; return View("Edit", input); }
        var project = id == 0 ? new ReliefProject() : await db.ReliefProjects.FindAsync(id);
        if (project == null) return NotFound();
        var wasActive = project.IsActive;
        project.Name = input.Name.Trim(); project.Location = input.Location; project.Description = input.Description;
        project.Category = input.Category; project.TargetAmount = input.TargetAmount; project.IsActive = input.IsActive; project.ImageUrl = input.ImageUrl;
        if (id == 0) db.ReliefProjects.Add(project);
        // Keep the create and audit write in one relational transaction.
        await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
        await db.SaveChangesAsync();
        experience.Audit(User, id == 0 ? "Created" : wasActive && !project.IsActive ? "Closed" : "Updated", "Project", project.Id, $"{project.Name} ({(project.IsActive ? "active" : "closed")}).");
        await db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
        TempData["Flash"] = "Project saved. Closed projects retain their history.";
        return RedirectToAction(nameof(Index));
    }
}
