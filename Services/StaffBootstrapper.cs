using System.ComponentModel.DataAnnotations;
using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Helpers.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Services;

public sealed class StaffBootstrapOptions
{
    public bool Enabled { get; set; }
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public bool ResetPassword { get; set; }
}

public enum StaffBootstrapStatus { Disabled, Created, Promoted, AlreadyConfigured, PasswordReset, Failed }
public sealed record StaffBootstrapResult(StaffBootstrapStatus Status, string? ErrorCode = null);

// Controlled by deployment settings only. Public registration never calls this service.
public sealed class StaffBootstrapper(
    ApplicationDbContext db, UserManager<IdentityUser> users,
    RoleManager<IdentityRole> roles, ILogger<StaffBootstrapper> logger)
{
    public async Task<StaffBootstrapResult> InitializeAsync(StaffBootstrapOptions options)
    {
        if (!options.Enabled) return new(StaffBootstrapStatus.Disabled);
        var email = options.Email.Trim();
        if (email.Length > 254 || !new EmailAddressAttribute().IsValid(email)) return Fail("InvalidEmail");
        if (!IsStrongPassword(options.Password)) return Fail("WeakPassword");

        try
        {
            var user = await users.FindByEmailAsync(email) ?? await users.FindByNameAsync(email);
            var isStaff = user != null && await users.IsInRoleAsync(user, "Staff");
            var passwordMatches = user != null && await users.CheckPasswordAsync(user, options.Password);
            // An existing public account cannot be silently promoted or have its password replaced.
            if (user != null && !isStaff && !passwordMatches) return Fail("ExistingAccountPasswordMismatch");
            if (user != null && isStaff && !passwordMatches && !options.ResetPassword) return Fail("ExistingStaffPasswordMismatch");
            if (isStaff && passwordMatches && !options.ResetPassword)
            {
                logger.LogInformation("Staff bootstrap: account already configured. Disable StaffBootstrap__Enabled and remove its password setting.");
                return new(StaffBootstrapStatus.AlreadyConfigured);
            }

            await using var transaction = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync() : null;
            if (!await roles.RoleExistsAsync("Staff"))
            {
                var roleResult = await roles.CreateAsync(new IdentityRole("Staff"));
                if (!roleResult.Succeeded) return Fail(Codes(roleResult));
            }

            StaffBootstrapStatus outcome;
            if (user == null)
            {
                user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
                var created = await users.CreateAsync(user, options.Password);
                if (!created.Succeeded) return Fail(Codes(created));
                outcome = StaffBootstrapStatus.Created;
            }
            else
            {
                outcome = isStaff ? StaffBootstrapStatus.AlreadyConfigured : StaffBootstrapStatus.Promoted;
                if (isStaff && options.ResetPassword)
                {
                    if (!passwordMatches)
                    {
                        var token = await users.GeneratePasswordResetTokenAsync(user);
                        var reset = await users.ResetPasswordAsync(user, token, options.Password);
                        if (!reset.Succeeded) return Fail(Codes(reset));
                    }
                    var unlocked = await users.SetLockoutEndDateAsync(user, null);
                    if (!unlocked.Succeeded) return Fail(Codes(unlocked));
                    var cleared = await users.ResetAccessFailedCountAsync(user);
                    if (!cleared.Succeeded) return Fail(Codes(cleared));
                    outcome = StaffBootstrapStatus.PasswordReset;
                }
            }
            if (!isStaff)
            {
                var assigned = await users.AddToRoleAsync(user, "Staff");
                if (!assigned.Succeeded)
                {
                    // In-memory stores have no transaction; avoid leaving a newly created unprivileged account.
                    if (transaction == null && outcome == StaffBootstrapStatus.Created) await users.DeleteAsync(user);
                    return Fail(Codes(assigned));
                }
            }
            if (outcome != StaffBootstrapStatus.AlreadyConfigured)
            {
                db.StaffAuditEntries.Add(new StaffAuditEntry
                {
                    ActorName = "Application setup", Action = "Staff bootstrap", EntityType = "IdentityUser", EntityId = 0,
                    Summary = $"{outcome}: configured staff account {email}."
                });
                await db.SaveChangesAsync();
            }
            if (transaction != null) await transaction.CommitAsync();
            logger.LogInformation("Staff bootstrap completed: {Outcome}. Disable StaffBootstrap__Enabled and remove its password setting.", outcome);
            return new(outcome);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A setup mistake must not recreate the deployment's 500.30 startup failure.
            logger.LogError("Staff bootstrap failed with {ExceptionType}. Check database access and StaffBootstrap settings; existing accounts remain available.", ex.GetType().Name);
            return new(StaffBootstrapStatus.Failed, "UnexpectedSetupError");
        }
    }

    private StaffBootstrapResult Fail(string code)
    {
        logger.LogError("Staff bootstrap failed: {Code}. See README production staff setup instructions. Passwords are never logged.", code);
        return new(StaffBootstrapStatus.Failed, code);
    }
    private static string Codes(IdentityResult result) => string.Join(",", result.Errors.Select(e => e.Code));
    private static bool IsStrongPassword(string password) => !string.IsNullOrEmpty(password) && password.Length >= 12
        && password.Any(char.IsUpper) && password.Any(char.IsLower) && password.Any(char.IsDigit)
        && password.Any(c => !char.IsLetterOrDigit(c));
}
