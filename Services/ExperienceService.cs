using System.Security.Claims;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Helpers.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
namespace GiftOfTheGivers.Services;

public sealed class ExperienceService(ApplicationDbContext db)
{
    // Queue and audit records share the caller's SaveChanges transaction.
    public void Notify(string? userId, string email, string subject, string body) =>
        db.Notifications.Add(new Notification { UserId = userId, Email = email, Subject = subject, Body = body });
    public void Audit(ClaimsPrincipal actor, string action, string type, int id, string summary) =>
        db.StaffAuditEntries.Add(new StaffAuditEntry
        {
            ActorUserId = actor.FindFirstValue(ClaimTypes.NameIdentifier), ActorName = actor.Identity?.Name ?? "Staff",
            Action = action, EntityType = type, EntityId = id, Summary = summary
        });
    public async Task<List<ProjectCard>> ProjectCardsAsync(bool activeOnly = true)
    {
        var projects = await db.ReliefProjects.AsNoTracking().Where(p => !activeOnly || p.IsActive).OrderBy(p => p.Name).ToListAsync();
        var amounts = await db.Donations.AsNoTracking().Where(d => d.ReliefProjectId != null && d.Currency == "ZAR")
            .Select(d => new { d.ReliefProjectId, d.Amount }).ToListAsync();
        return projects.Select(p => new ProjectCard
        {
            Project = p, RecordedZar = amounts.Where(d => d.ReliefProjectId == p.Id).Sum(d => d.Amount)
        }).ToList();
    }
    public static async Task<PagedResult<T>> PageAsync<T>(IQueryable<T> query, int page, int size = 12)
    {
        var count = await query.CountAsync();
        page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(count / (double)size)));
        return new PagedResult<T> { Page = page, PageSize = size, TotalCount = count, Items = await query.Skip((page - 1) * size).Take(size).ToListAsync() };
    }
}
