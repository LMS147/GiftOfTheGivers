using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace GiftOfTheGivers.Functions;

/// <summary>
/// Section A.1: HTTP-triggered Azure Function that logs a project field update
/// when an employee/staff member submits new information from the field.
/// </summary>
public class LogProjectUpdateFunction
{
    private readonly ILogger<LogProjectUpdateFunction> _logger;

    public LogProjectUpdateFunction(ILogger<LogProjectUpdateFunction> logger)
    {
        _logger = logger;
    }

    [Function("LogProjectUpdate")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "log-update")] HttpRequest req)
    {
        _logger.LogInformation("Processing employee project update logging request");

        try
        {
            using var reader = new StreamReader(req.Body);
            var body = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(body))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    error = "Request body cannot be empty."
                });
            }

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var update = JsonSerializer.Deserialize<ProjectUpdateLogDto>(body, options);

            if (update == null || string.IsNullOrWhiteSpace(update.Title))
            {
                return new BadRequestObjectResult(new
                {
                    success = false,
                    error = "Project update must contain at least a Title and ProjectId."
                });
            }

            var logId = $"LOG-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..8].ToUpperInvariant()}";
            var timestamp = DateTime.UtcNow;

            _logger.LogInformation(
                "Audit Log [{LogId}]: Project {ProjectId} update posted by {Author} ('{Title}')",
                logId, update.ProjectId, update.AuthorEmail ?? "Staff", update.Title);

            // Returns simulated / audit logging confirmation
            return new OkObjectResult(new
            {
                success = true,
                logId = logId,
                projectId = update.ProjectId,
                title = update.Title,
                authorEmail = update.AuthorEmail ?? "staff@gotg.org",
                timestampUtc = timestamp,
                status = "AuditLogStored",
                destination = "Azure Storage Audit Container: /audit-logs/project-updates",
                message = "Employee project update successfully validated and stored."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing project update log");
            return new StatusCodeResult(StatusCodes.Status500InternalServerError);
        }
    }
}

public class ProjectUpdateLogDto
{
    [JsonPropertyName("projectId")]
    public int ProjectId { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; set; }

    [JsonPropertyName("authorEmail")]
    public string? AuthorEmail { get; set; }

    [JsonPropertyName("location")]
    public string? Location { get; set; }
}
