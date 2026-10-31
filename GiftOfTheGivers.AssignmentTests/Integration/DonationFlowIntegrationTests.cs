using System.Text.Json;
using GiftOfTheGivers.AssignmentTests.Infrastructure;
using Xunit;
using Xunit.Abstractions;
namespace GiftOfTheGivers.AssignmentTests.Integration;

[Collection("Assignment system"), Trait("Category", "Integration")]
public class DonationFlowIntegrationTests(AssignmentEnvironment environment, ITestOutputHelper output)
{
    [Fact]
    public async Task AnonymousFormProducesAStoredDonationAndARealFunctionCertificate()
    {
        var result = await DonationFlow.SubmitAndVerifyAsync(environment);
        var evidence = new
        {
            testedAtUtc = DateTime.UtcNow, result.DonationId, result.SubmissionKey, result.CertificateNumber,
            result.StoredAmount, result.CloudGenerated,
            assertions = new[] { "Form validation and antiforgery", "Review and confirmation", "SQL Server persistence", "Real HTTP Azure Function response", "Function reads the same saved record", "No local fallback accepted" },
            architecture = "Web form -> SQL write -> certificate service -> Azure Function -> shared SQL read -> certificate response"
        };
        await File.WriteAllTextAsync(Path.Combine(environment.EvidenceDirectory, "integration-result.json"), JsonSerializer.Serialize(evidence, new JsonSerializerOptions { WriteIndented = true }));
        output.WriteLine("Real Function certificate returned for SQL record {0}: {1}", result.DonationId, result.CertificateNumber);
        output.WriteLine("Evidence: {0}", environment.EvidenceDirectory);
    }
}
