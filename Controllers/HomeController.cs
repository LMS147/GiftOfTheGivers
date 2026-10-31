using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Helpers.Models.ViewModels;

namespace GiftOfTheGivers.Controllers;

public class HomeController : Controller
{
    private readonly HttpClient _httpClient;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IHttpClientFactory httpClientFactory,
        ApplicationDbContext db,
        ILogger<HomeController> logger)
    {
        _httpClient = httpClientFactory.CreateClient("GiftOfTheGiversFunctions");
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        // 1. Try fetching home metrics from Azure Function: GET /api/home/summary
        if (_httpClient.BaseAddress != null) try
        {
            var summary = await _httpClient.GetFromJsonAsync<HomeViewModel>("api/home/summary");
            if (summary != null)
            {
                return View(summary);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Function offline — calculating metrics via direct DbContext.");
        }

        // 2. Direct DbContext fallback
        var zarAmounts = await _db.Donations
            .Where(d => d.Currency == "ZAR")
            .Select(d => d.Amount)
            .ToListAsync();

        var fallbackVm = new HomeViewModel
        {
            LatestUpdates = await _db.ProjectUpdates
                .Include(u => u.ReliefProject)
                .OrderByDescending(u => u.PostedAt)
                .Take(3)
                .ToListAsync(),
            DonationCount = await _db.Donations.CountAsync(),
            TotalDonatedZar = DonationMetricsHelper.CalculateTotal(zarAmounts),
            VolunteerCount = await _db.Volunteers.CountAsync(),
            ProjectCount = await _db.ReliefProjects.CountAsync(),
        };

        return View(fallbackVm);
    }

    public IActionResult About() => View();

    public IActionResult Contact() => View();

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}

