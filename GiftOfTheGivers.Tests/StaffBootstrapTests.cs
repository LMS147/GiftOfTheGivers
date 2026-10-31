using GiftOfTheGivers.Helpers.Data;
using GiftOfTheGivers.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
namespace GiftOfTheGivers.Tests;

public class StaffBootstrapTests
{
    private const string OriginalPassword = "Original!Staff2026";
    private const string NewPassword = "Replacement!Staff2026";

    [Fact]
    public async Task DisabledSetupDoesNotCreateAccounts()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<StaffBootstrapper>().InitializeAsync(new());
        Assert.Equal(StaffBootstrapStatus.Disabled, result.Status);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.ToListAsync());
    }
    [Theory]
    [InlineData("not-an-email", OriginalPassword, "InvalidEmail")]
    [InlineData("staff@example.com", "weak", "WeakPassword")]
    [InlineData("staff@example.com", "Password123!", "WeakPassword")]
    public async Task InvalidSettingsLeaveDatabaseUnchanged(string email, string password, string expected)
    {
        using var services = Services(); using var scope = services.CreateScope();
        var result = await scope.ServiceProvider.GetRequiredService<StaffBootstrapper>().InitializeAsync(new() { Enabled = true, Email = email, Password = password });
        Assert.Equal(expected, result.ErrorCode);
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Users.ToListAsync());
    }
    [Fact]
    public async Task CreatesStaffWithHashedPasswordAndRepeatedStartupDoesNotChangeIt()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var setup = scope.ServiceProvider.GetRequiredService<StaffBootstrapper>();
        var options = new StaffBootstrapOptions { Enabled = true, Email = "staff@example.com", Password = OriginalPassword };
        Assert.Equal(StaffBootstrapStatus.Created, (await setup.InitializeAsync(options)).Status);
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByEmailAsync(options.Email))!;
        Assert.True(await users.IsInRoleAsync(user, "Staff")); Assert.True(await users.CheckPasswordAsync(user, OriginalPassword));
        Assert.NotEqual(OriginalPassword, user.PasswordHash);
        var hash = user.PasswordHash;
        Assert.Equal(StaffBootstrapStatus.AlreadyConfigured, (await setup.InitializeAsync(options)).Status);
        Assert.Equal(hash, user.PasswordHash);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await db.Users.ToListAsync()); Assert.Single(await db.StaffAuditEntries.ToListAsync());
    }
    [Fact]
    public async Task DoesNotPromoteAnExistingPublicAccountWithAnotherPassword()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = "donor@example.com", Email = "donor@example.com" };
        Assert.True((await users.CreateAsync(user, OriginalPassword)).Succeeded);
        var result = await scope.ServiceProvider.GetRequiredService<StaffBootstrapper>().InitializeAsync(new() { Enabled = true, Email = user.Email, Password = NewPassword, ResetPassword = true });
        Assert.Equal("ExistingAccountPasswordMismatch", result.ErrorCode);
        Assert.False(await users.IsInRoleAsync(user, "Staff")); Assert.True(await users.CheckPasswordAsync(user, OriginalPassword));
    }
    [Fact]
    public async Task ExistingStaffPasswordChangesOnlyWithExplicitRecoveryAndUnlocksAccount()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var setup = scope.ServiceProvider.GetRequiredService<StaffBootstrapper>();
        await setup.InitializeAsync(new() { Enabled = true, Email = "staff@example.com", Password = OriginalPassword });
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = (await users.FindByEmailAsync("staff@example.com"))!;
        await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddDays(1));
        var options = new StaffBootstrapOptions { Enabled = true, Email = "staff@example.com", Password = NewPassword };
        Assert.Equal("ExistingStaffPasswordMismatch", (await setup.InitializeAsync(options)).ErrorCode);
        Assert.True(await users.CheckPasswordAsync(user, OriginalPassword));
        options.ResetPassword = true;
        Assert.Equal(StaffBootstrapStatus.PasswordReset, (await setup.InitializeAsync(options)).Status);
        Assert.True(await users.CheckPasswordAsync(user, NewPassword)); Assert.False(await users.CheckPasswordAsync(user, OriginalPassword));
        Assert.Null(await users.GetLockoutEndDateAsync(user));
    }
    [Fact]
    public async Task ExistingPublicAccountCanBePromotedWithItsCurrentPassword()
    {
        using var services = Services(); using var scope = services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var user = new IdentityUser { UserName = "donor@example.com", Email = "donor@example.com" };
        await users.CreateAsync(user, OriginalPassword); var hash = user.PasswordHash;
        var result = await scope.ServiceProvider.GetRequiredService<StaffBootstrapper>().InitializeAsync(new() { Enabled = true, Email = user.Email, Password = OriginalPassword });
        Assert.Equal(StaffBootstrapStatus.Promoted, result.Status); Assert.True(await users.IsInRoleAsync(user, "Staff")); Assert.Equal(hash, user.PasswordHash);
    }
    private static ServiceProvider Services()
    {
        var services = new ServiceCollection();
        services.AddLogging(); services.AddDataProtection();
        services.AddDbContext<ApplicationDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
        services.AddIdentity<IdentityUser, IdentityRole>().AddEntityFrameworkStores<ApplicationDbContext>().AddDefaultTokenProviders();
        services.AddScoped<StaffBootstrapper>();
        return services.BuildServiceProvider();
    }
}
