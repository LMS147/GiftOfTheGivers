using System.Linq.Expressions;
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

    // Project to acyclic response objects instead of serializing tracked navigation graphs.
    private static readonly Expression<Func<ProjectUpdate, ProjectUpdate>> UpdateResponse = u => new ProjectUpdate
    {
        Id = u.Id, ReliefProjectId = u.ReliefProjectId, Title = u.Title, Body = u.Body,
        PostedAt = u.PostedAt, PostedByName = u.PostedByName,
        ReliefProject = u.ReliefProject == null ? null : new ReliefProject
        {
            Id = u.ReliefProject.Id, Name = u.ReliefProject.Name,
            Description = u.ReliefProject.Description, Location = u.ReliefProject.Location
        }
    };

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
            .OrderByDescending(u => u.PostedAt)
            .Take(3)
            .Select(UpdateResponse)
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
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                p.Id, p.Name, p.Location, p.Description,
                Updates = p.Updates.Select(u => new
                {
                    u.Id, u.ReliefProjectId, u.Title, u.Body, u.PostedAt, u.PostedByName
                }).ToList()
            })
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
            .OrderByDescending(u => u.PostedAt)
            .Select(UpdateResponse)
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(updates);
        return response;
    }
}

