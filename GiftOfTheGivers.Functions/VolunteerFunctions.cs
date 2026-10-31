using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Net;
using System.ComponentModel.DataAnnotations;

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
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "volunteers")] HttpRequestData req)
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
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "volunteers/{id:int}")] HttpRequestData req, int id)
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
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "volunteers")] HttpRequestData req)
    {
        var volunteer = await req.ReadFromJsonAsync<Volunteer>();

        if (volunteer == null || volunteer.Email.Length > 254 || !Validator.TryValidateObject(volunteer, new ValidationContext(volunteer), new List<ValidationResult>(), true))
        {
            return req.CreateResponse(HttpStatusCode.BadRequest);
        }

        if (volunteer.SignUpDate == default)
        {
            volunteer.SignUpDate = DateTime.Now;
        }

        volunteer.Id = 0;
        volunteer.SignUpDate = DateTime.UtcNow;
        volunteer.Status = "Pending";
        _context.Notifications.Add(new Notification { UserId = volunteer.UserId, Email = volunteer.Email, Subject = "Volunteer application received", Body = "Your application is pending review. Our team will contact you after a decision." });
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
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "volunteers/{id:int}/status")] HttpRequestData req, int id)
    {
        var volunteer = await _context.Volunteers.FindAsync(id);

        if (volunteer == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        var statusUpdate = await req.ReadFromJsonAsync<VolunteerStatusUpdateDto>();
        if (statusUpdate?.Status is not ("Pending" or "Approved" or "Declined"))
            return req.CreateResponse(HttpStatusCode.BadRequest);
        if (statusUpdate != null)
        {
            if (volunteer.Status != statusUpdate.Status)
            {
                var previous = volunteer.Status;
                volunteer.Status = statusUpdate.Status;
                _context.Notifications.Add(new Notification { UserId = volunteer.UserId, Email = volunteer.Email, Subject = $"Volunteer application: {volunteer.Status}", Body = $"Your application status is now {volunteer.Status}. Contact the support team if you need help with next steps." });
                _context.StaffAuditEntries.Add(new StaffAuditEntry { ActorName = "Trusted API", Action = "Status changed", EntityType = "Volunteer", EntityId = id, Summary = $"Application changed from {previous} to {volunteer.Status}." });
                await _context.SaveChangesAsync();
            }
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
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "volunteers/{id:int}")] HttpRequestData req, int id)
    {
        var volunteer = await _context.Volunteers.FindAsync(id);

        if (volunteer == null)
        {
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        _context.StaffAuditEntries.Add(new StaffAuditEntry { ActorName = "Trusted API", Action = "Deleted", EntityType = "Volunteer", EntityId = id, Summary = "Deleted volunteer application through trusted API." });
        _context.Volunteers.Remove(volunteer);
        await _context.SaveChangesAsync();

        return req.CreateResponse(HttpStatusCode.NoContent);
    }
}

public class VolunteerStatusUpdateDto
{
    public string Status { get; set; } = string.Empty;
}


