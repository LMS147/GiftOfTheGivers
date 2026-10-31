using System.Security.Cryptography;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using GiftOfTheGivers.Helpers.Models.ViewModels;
using Microsoft.AspNetCore.DataProtection;
namespace GiftOfTheGivers.Services;

public sealed class DonationReviewService(IDataProtectionProvider provider)
{
    private readonly IDataProtector _protector = provider.CreateProtector("GiftOfTheGivers.DonationReview.v1");
    private sealed record Draft(DonationInput Input, string Owner, DateTime ExpiresAtUtc);
    public string Protect(DonationInput input, string owner) =>
        _protector.Protect(JsonSerializer.Serialize(new Draft(input, owner, DateTime.UtcNow.AddMinutes(30))));
    public DonationInput? Read(string? token, string owner)
    {
        if (string.IsNullOrWhiteSpace(token)) return null;
        try
        {
            var draft = JsonSerializer.Deserialize<Draft>(_protector.Unprotect(token));
            if (draft == null || draft.Owner != owner || draft.ExpiresAtUtc < DateTime.UtcNow || draft.Input.SubmissionKey == Guid.Empty) return null;
            return Validator.TryValidateObject(draft.Input, new ValidationContext(draft.Input), new List<ValidationResult>(), true) ? draft.Input : null;
        }
        catch (Exception ex) when (ex is CryptographicException or JsonException or ArgumentException) { return null; }
    }
}
