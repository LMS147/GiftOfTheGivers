using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models.ViewModels;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace GiftOfTheGivers.Controllers;

public class ProjectsController(ApplicationDbContext db, ExperienceService experience) : Controller
{
    public async Task<IActionResult> Index(string? q, string? category, string? status, int page = 1)
    {
        var cards = (await experience.ProjectCardsAsync(false)).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(q)) cards = cards.Where(c => c.Project.Name.Contains(q, StringComparison.OrdinalIgnoreCase) || (c.Project.Location?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false));
        if (!string.IsNullOrWhiteSpace(category)) cards = cards.Where(c => c.Project.Category == category);
        cards = status == "closed" ? cards.Where(c => !c.Project.IsActive) : cards.Where(c => c.Project.IsActive);
        var list = cards.ToList(); page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(list.Count / 9d)));
        return View(new PagedResult<ProjectCard> { Items = list.Skip((page - 1) * 9).Take(9).ToList(), TotalCount = list.Count, Page = page, PageSize = 9 });
    }
    public async Task<IActionResult> Details(int id)
    {
        var card = (await experience.ProjectCardsAsync(false)).FirstOrDefault(c => c.Project.Id == id);
        if (card == null) return NotFound();
        return View(new ProjectDetailsViewModel { Card = card, Updates = await db.ProjectUpdates.AsNoTracking().Where(u => u.ReliefProjectId == id).OrderByDescending(u => u.PostedAt).Take(10).ToListAsync() });
    }
}
