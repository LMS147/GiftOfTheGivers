using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

namespace GiftOfTheGivers.Services;

// Guest access is a short-lived, protected browser cookie, never a guessable ID.
public sealed class SubmissionAccessService
{
    private readonly IDataProtector _protector;
    public SubmissionAccessService(IDataProtectionProvider provider) =>
        _protector = provider.CreateProtector("GiftOfTheGivers.SubmissionAccess.v1");

    private static string CookieName(string kind, int id) => $"gotg-{kind}-{id}";

    public void Grant(HttpContext context, string kind, int id)
    {
        var expires = DateTimeOffset.UtcNow.AddDays(1);
        var payload = $"{kind}:{id}:{expires.ToUnixTimeSeconds()}";
        context.Response.Cookies.Append(CookieName(kind, id), _protector.Protect(payload), new CookieOptions
        {
            HttpOnly = true, Secure = context.Request.IsHttps,
            SameSite = SameSiteMode.Lax, IsEssential = true, Expires = expires, Path = "/"
        });
    }

    public bool CanRead(HttpContext context, string kind, int id, string? ownerId)
    {
        if (context.User.IsInRole("Staff")) return true;
        var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (context.User.Identity?.IsAuthenticated == true && !string.IsNullOrEmpty(ownerId) && ownerId == userId)
            return true;
        // A browser grant only authorizes anonymous submissions, never another account's data.
        if (!string.IsNullOrEmpty(ownerId)) return false;
        if (!context.Request.Cookies.TryGetValue(CookieName(kind, id), out var token)) return false;
        try
        {
            var parts = _protector.Unprotect(token).Split(':');
            return parts.Length == 3 && parts[0] == kind && parts[1] == id.ToString(CultureInfo.InvariantCulture)
                && long.TryParse(parts[2], out var expiry) && expiry > DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }
        catch (CryptographicException) { return false; }
    }
}
