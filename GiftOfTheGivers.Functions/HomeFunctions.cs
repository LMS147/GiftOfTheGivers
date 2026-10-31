using System.Net;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers;
using GiftOfTheGivers.Helpers.Models;
using GiftOfTheGivers.Helpers.Models.ViewModels;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Functions;

public class HomeFunctions
{
    // ============================================================
    // Declare the database context (housed in the Class Library).
    // ============================================================
    private readonly ApplicationDbContext _context;

    public HomeFunctions(ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // READ HOME PAGE SUMMARY & METRICS
    // HTTP METHOD: GET
    // URL: /api/home/summary
    // CRUD OPERATION: READ
    // ============================================================
    [Function("GetHomeSummary")]
    public async Task<HttpResponseData> GetHomeSummary(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "home/summary")] HttpRequestData req)
    {
        // 1. Fetch latest field updates
        var latestUpdates = await _context.ProjectUpdates
            .Include(u => u.ReliefProject)
            .OrderByDescending(u => u.PostedAt)
            .Take(3)
            .ToListAsync();

        // 2. Fetch counts
        var donationCount = await _context.Donations.CountAsync();
        var volunteerCount = await _context.Volunteers.CountAsync();
        var projectCount = await _context.ReliefProjects.CountAsync();

        // 3. Compute total funds raised in ZAR using Class Library helper
        var zarAmounts = await _context.Donations
            .Where(d => d.Currency == "ZAR")
            .Select(d => d.Amount)
            .ToListAsync();
        var totalDonatedZar = DonationMetricsHelper.CalculateTotal(zarAmounts);

        var homeViewModel = new HomeViewModel
        {
            LatestUpdates = latestUpdates,
            DonationCount = donationCount,
            TotalDonatedZar = totalDonatedZar,
            VolunteerCount = volunteerCount,
            ProjectCount = projectCount
        };

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(homeViewModel);
        return response;
    }

    // ============================================================
    // READ ALL RELIEF PROJECTS
    // HTTP METHOD: GET
    // URL: /api/projects
    // CRUD OPERATION: READ
    // ============================================================
    [Function("GetProjects")]
    public async Task<HttpResponseData> GetProjects(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "projects")] HttpRequestData req)
    {
        var projects = await _context.ReliefProjects
            .Include(p => p.Updates)
            .OrderBy(p => p.Name)
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(projects);
        return response;
    }

    // ============================================================
    // READ ALL FIELD UPDATES
    // HTTP METHOD: GET
    // URL: /api/updates
    // CRUD OPERATION: READ
    // ============================================================
    [Function("GetUpdates")]
    public async Task<HttpResponseData> GetUpdates(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "updates")] HttpRequestData req)
    {
        var updates = await _context.ProjectUpdates
            .Include(u => u.ReliefProject)
            .OrderByDescending(u => u.PostedAt)
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(updates);
        return response;
    }
}

