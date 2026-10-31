using System.Security.Claims;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace GiftOfTheGivers.Controllers;

[Authorize, ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class NotificationsController(ApplicationDbContext db) : Controller
{
    public async Task<IActionResult> Index(int page = 1) => View(await ExperienceService.PageAsync(db.Notifications.AsNoTracking()
        .Where(n => n.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier)).OrderByDescending(n => n.CreatedAtUtc), page));
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var item = await db.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == User.FindFirstValue(ClaimTypes.NameIdentifier));
        if (item == null) return NotFound();
        item.ReadAtUtc ??= DateTime.UtcNow; await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }
}
