using System.Security.Claims;
using GiftOfTheGivers.Helpers.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace GiftOfTheGivers.ViewComponents;
public class NotificationBellViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        var id = HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = id == null ? 0 : await db.Notifications.CountAsync(n => n.UserId == id && n.ReadAtUtc == null);
        return View(unread);
    }
}
