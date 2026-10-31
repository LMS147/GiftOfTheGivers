using System.Security.Claims;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace GiftOfTheGivers.Controllers;

[Authorize(Roles = "Donor"), ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class DonorController(DonorDashboardService dashboard) : Controller
{
    public async Task<IActionResult> Index(string? currency)
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(id)) return Challenge();
        return View(await dashboard.BuildAsync(id, currency, User.Identity?.Name));
    }
}
