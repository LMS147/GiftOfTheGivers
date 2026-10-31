using System.Reflection;
using GiftOfTheGivers.Functions;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace GiftOfTheGivers.Tests;

public class FunctionTests
{
    [Fact]
    public void AllHttpFunctionsRequireAFunctionKey()
    {
        var triggers = typeof(DonationFunctions).Assembly.GetTypes().SelectMany(t => t.GetMethods())
            .SelectMany(m => m.GetParameters()).Select(p => p.GetCustomAttribute<HttpTriggerAttribute>())
            .Where(a => a != null).ToList();
        Assert.NotEmpty(triggers);
        Assert.All(triggers, t => Assert.Equal(AuthorizationLevel.Function, t!.AuthLevel));
    }

    [Fact]
    public async Task CertificateRejectsInvalidAndUnknownDonationIds()
    {
        await using var db = Database();
        var function = new GenerateTaxCertificateFunction(db);
        var request = new DefaultHttpContext().Request;
        request.Method = "GET";
        Assert.IsType<BadRequestObjectResult>(await function.Run(request));
        request.QueryString = new QueryString("?donationId=42");
        Assert.IsType<NotFoundResult>(await function.Run(request));
    }

    [Fact]
    public async Task MissingAuditStorageDoesNotReportSuccess()
    {
        await using var db = Database();
        db.ProjectUpdates.Add(new ProjectUpdate { Id = 1, Title = "Update", Body = "Details" });
        await db.SaveChangesAsync();
        var function = new LogProjectUpdateFunction(db, new ConfigurationBuilder().Build(), NullLogger<LogProjectUpdateFunction>.Instance);
        var request = new DefaultHttpContext().Request;
        request.Body = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("{\"updateId\":1}"));
        var result = Assert.IsType<ObjectResult>(await function.Run(request));
        Assert.Equal(503, result.StatusCode);
    }

    [Fact]
    public void MigrationSnapshotMatchesCurrentModel()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer("Server=localhost;Database=unused;Trusted_Connection=True;TrustServerCertificate=True").Options);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private static ApplicationDbContext Database() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
}
