using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;

namespace GiftOfTheGivers.Functions;

public class VolunteerFunctions
{
    // ============================================================
    // Declare the database context (housed in the Class Library).
    // This will be used by all the functions to access the Volunteers table.
    // ============================================================
    private readonly ApplicationDbContext _context;

    // Constructor: ApplicationDbContext is injected using Dependency Injection.
    public VolunteerFunctions(ApplicationDbContext context)
    {
        _context = context;
    }

    // ============================================================
    // READ ALL VOLUNTEERS
    // HTTP METHOD: GET
    // URL: /api/volunteers
    // CRUD OPERATION: READ
    // ============================================================
    [Function("GetVolunteers")]
    public async Task<HttpResponseData> GetVolunteers(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "volunteers")] HttpRequestData req)
    {
        var volunteers = await _context.Volunteers
            .OrderByDescending(v => v.SignUpDate)
            .ToListAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(volunteers);
        return response;
    }

    // ============================================================
    // READ ONE VOLUNTEER
    // HTTP METHOD: GET
    // Example URL: /api/volunteers/5
    // CRUD OPERATION: READ
    // ============================================================
    [Function("GetVolunteerById")]
    public async Task<HttpResponseData> GetVolunteerById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "volunteers/{id:int}")] HttpRequestData req, int id)
    {
        var volunteer = await _context.Volunteers.FindAsync(id);

        if (volunteer == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(volunteer);
        return response;
    }

    // ============================================================
    // CREATE A NEW VOLUNTEER
    // HTTP METHOD: POST
    // URL: /api/volunteers
    // CRUD OPERATION: CREATE
    // ============================================================
    [Function("CreateVolunteer")]
    public async Task<HttpResponseData> CreateVolunteer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "volunteers")] HttpRequestData req)
    {
        var volunteer = await req.ReadFromJsonAsync<Volunteer>();

        if (volunteer == null || string.IsNullOrWhiteSpace(volunteer.FullName))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (volunteer.SignUpDate == default)
        {
            volunteer.SignUpDate = DateTime.Now;
        }

        _context.Volunteers.Add(volunteer);
        await _context.SaveChangesAsync();

        var response = req.CreateResponse(HttpStatusCode.Created);
        await response.WriteAsJsonAsync(volunteer);
        return response;
    }

    // ============================================================
    // UPDATE VOLUNTEER STATUS
    // HTTP METHOD: PUT
    // Example URL: /api/volunteers/5/status
    // CRUD OPERATION: UPDATE
    // ============================================================
    [Function("UpdateVolunteerStatus")]
    public async Task<HttpResponseData> UpdateVolunteerStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "volunteers/{id:int}/status")] HttpRequestData req, int id)
    {
        var volunteer = await _context.Volunteers.FindAsync(id);

        if (volunteer == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var statusUpdate = await req.ReadFromJsonAsync<VolunteerStatusUpdateDto>();
        if (statusUpdate != null && !string.IsNullOrWhiteSpace(statusUpdate.Status))
        {
            volunteer.Status = statusUpdate.Status;
            await _context.SaveChangesAsync();
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(volunteer);
        return response;
    }

    // ============================================================
    // DELETE A VOLUNTEER
    // HTTP METHOD: DELETE
    // Example URL: /api/volunteers/5
    // CRUD OPERATION: DELETE
    // ============================================================
    [Function("DeleteVolunteer")]
    public async Task<HttpResponseData> DeleteVolunteer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "volunteers/{id:int}")] HttpRequestData req, int id)
    {
        var volunteer = await _context.Volunteers.FindAsync(id);

        if (volunteer == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        _context.Volunteers.Remove(volunteer);
        await _context.SaveChangesAsync();

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}

public class VolunteerStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
}

