using Azure.Storage.Blobs;
using GiftOfTheGivers.Helpers.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace GiftOfTheGivers.Functions;

public class LogProjectUpdateFunction
{
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<LogProjectUpdateFunction> _logger;
    public LogProjectUpdateFunction(ApplicationDbContext db, IConfiguration config, ILogger<LogProjectUpdateFunction> logger)
    { _db = db; _config = config; _logger = logger; }

    [Function("LogProjectUpdate")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "log-update")] HttpRequest req)
    {
        AuditRequest? payload;
        try { payload = await JsonSerializer.DeserializeAsync<AuditRequest>(req.Body,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch (JsonException) { return new BadRequestObjectResult("Invalid JSON."); }
        if (payload == null || payload.UpdateId <= 0) return new BadRequestObjectResult("A positive updateId is required.");
        var update = await _db.ProjectUpdates.FindAsync(payload.UpdateId);
        if (update == null) return new NotFoundResult();
        var connection = _config["AuditStorageConnectionString"];
        if (string.IsNullOrWhiteSpace(connection))
            return new ObjectResult(new { success = false, error = "Audit storage is not configured." }) { StatusCode = 503 };
        try
        {
            var container = new BlobContainerClient(connection, "project-update-audits");
            await container.CreateIfNotExistsAsync(); // Private container by default.
            var blobName = $"update-{update.Id}.json";
            await container.GetBlobClient(blobName).UploadAsync(BinaryData.FromObjectAsJson(new
            {
                update.Id, update.ReliefProjectId, update.Title, update.Body, update.PostedByName, update.PostedAt
            }), overwrite: true);
            return new OkObjectResult(new { success = true, updateId = update.Id, status = "AuditLogStored", blobName });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not persist audit for update {UpdateId}", update.Id);
            return new StatusCodeResult(StatusCodes.Status503ServiceUnavailable);
        }
    }
    private sealed class AuditRequest { public int UpdateId { get; set; } }
}
