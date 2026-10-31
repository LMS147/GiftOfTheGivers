using System.Net;
using System.Text.RegularExpressions;
using System.Text.Json;
using GiftOfTheGivers.Helpers.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
namespace GiftOfTheGivers.AssignmentTests.Infrastructure;

public sealed record DonationFlowResult(int DonationId, Guid SubmissionKey, string CertificateNumber, decimal StoredAmount, bool CloudGenerated);

public static class DonationFlow
{
    public static string Hidden(string html, string name)
    {
        var input = Regex.Match(html, "<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*>", RegexOptions.IgnoreCase).Value;
        var value = Regex.Match(input, "value=\"([^\"]*)\"", RegexOptions.IgnoreCase).Groups[1].Value;
        Assert.False(string.IsNullOrWhiteSpace(value), "Missing hidden field: " + name);
        return WebUtility.HtmlDecode(value);
    }
    public static async Task<DonationFlowResult> SubmitAndVerifyAsync(AssignmentEnvironment environment)
    {
        using var client = environment.Client();
        var form = await client.GetStringAsync("/Donations/Create");
        var token = Hidden(form, "__RequestVerificationToken");
        var submission = Guid.Parse(Hidden(form, "SubmissionKey"));
        var review = await client.PostAsync("/Donations/Review", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token, ["SubmissionKey"] = submission.ToString(),
            ["DonorName"] = "Assignment Integration Donor", ["Email"] = "integration@example.test", ["Amount"] = "175.25", ["Currency"] = "ZAR", ["ReliefProjectId"] = environment.ProjectId.ToString()
        }));
        Assert.Equal(HttpStatusCode.OK, review.StatusCode);
        var reviewHtml = await review.Content.ReadAsStringAsync();
        var confirmation = await client.PostAsync("/Donations/Create", new FormUrlEncodedContent(new Dictionary<string,string>
        {
            ["__RequestVerificationToken"] = Hidden(reviewHtml, "__RequestVerificationToken"), ["reviewToken"] = Hidden(reviewHtml, "reviewToken")
        }));
        Assert.Equal(HttpStatusCode.Redirect, confirmation.StatusCode);
        var receipt = await client.GetAsync(confirmation.Headers.Location); Assert.Equal(HttpStatusCode.OK, receipt.StatusCode);
        await using var db = environment.Database();
        var row = await db.Donations.AsNoTracking().SingleAsync(d => d.SubmissionKey == submission);
        Assert.Equal(175.25m, row.Amount); Assert.Null(row.DonorId); Assert.Equal(environment.ProjectId, row.ReliefProjectId);
        // This endpoint calls the actual Functions host. Local fallback is explicitly rejected.
        using var certificate = JsonDocument.Parse(await client.GetStringAsync($"/Donations/CloudCertificate/{row.Id}"));
        var json = certificate.RootElement;
        Assert.True(json.GetProperty("isCloudGenerated").GetBoolean(), "The Function must answer; a local certificate fallback does not satisfy this test.");
        Assert.Equal(row.Id, json.GetProperty("donationId").GetInt32());
        Assert.Equal(row.ReferenceNumber, json.GetProperty("certificateNumber").GetString());
        using var functionClient = new HttpClient();
        using var persisted = JsonDocument.Parse(await functionClient.GetStringAsync(environment.FunctionUrl + $"/api/donations/{row.Id}"));
        var stored = persisted.RootElement.EnumerateObject().Single(p => p.Name.Equals("amount", StringComparison.OrdinalIgnoreCase)).Value.GetDecimal();
        Assert.Equal(row.Amount, stored);
        return new(row.Id, submission, row.ReferenceNumber, row.Amount, true);
    }
}
