using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using GiftOfTheGivers.Helpers.Models;

namespace GiftOfTheGivers.Helpers.Data;

/// <summary>
/// Seeds the database on first run: roles (Staff, Volunteer, Donor), demo
/// accounts and starting data. Safe to run on every startup — it only adds
/// missing data. (The schema itself is created in Program.cs — via EFs
/// migrations on SQL Server / EnsureCreated on SQLite.)
/// </summary>
public class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<IdentityUser>>();

        // --- Roles ----------------------------------------------------------
        foreach (var roleName in new[] { "Staff", "Volunteer", "Donor" })
        {
            if (!await roleManager.RoleExistsAsync(roleName))
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        // --- Migration helper: older databases called the staff role "Employee".
        // Rename it to "Staff" so role checks stay consistent. User links keep
        // working because the role Id does not change.
        var legacyRole = await roleManager.FindByNameAsync("Employee");
        if (legacyRole != null && !await roleManager.RoleExistsAsync("Staff"))
        {
            legacyRole.Name = "Staff";
            await roleManager.UpdateAsync(legacyRole);
        }

        // --- Demo accounts ---------------------------------------------------
        await CreateDemoUserAsync(userManager, "staff@gotg.org", "Password123!", "Staff");
        await CreateDemoUserAsync(userManager, "volunteer@gotg.org", "Password123!", "Volunteer");
        await CreateDemoUserAsync(userManager, "donor@gotg.org", "Password123!", "Donor");

        // --- Starting relief projects -----------------------------------------
        if (!await db.ReliefProjects.AnyAsync())
        {
            db.ReliefProjects.AddRange(
                new ReliefProject
                {
                    Name = "KZN Flood Relief",
                    Location = "KwaZulu-Natal",
                    Description = "Emergency food parcels, blankets and clean drinking water for communities affected by flooding."
                },
                new ReliefProject
                {
                    Name = "Winter Warmth Drive",
                    Location = "Gauteng & Free State",
                    Description = "Warm clothing, blankets and hot meals distributed through shelters during the winter months."
                },
                new ReliefProject
                {
                    Name = "Eastern Cape Drought Support",
                    Location = "Eastern Cape",
                    Description = "Water tanks, borehole repairs and fodder for farming communities facing drought."
                });
            await db.SaveChangesAsync();
        }

        // --- Volunteer applications (one for the demo volunteer, one pending) --
        if (!await db.Volunteers.AnyAsync())
        {
            var demoVolunteer = await userManager.FindByEmailAsync("volunteer@gotg.org");
            db.Volunteers.AddRange(
                new Volunteer
                {
                    FullName = "Demo Volunteer",
                    Email = "volunteer@gotg.org",
                    Phone = "082 000 0000",
                    Skills = "First Aid, Food Preparation",
                    Availability = "Weekends",
                    Notes = "Available to help with weekend distributions and community kitchens.",
                    Status = "Approved",
                    UserId = demoVolunteer?.Id
                },
                new Volunteer
                {
                    FullName = "Lerato Mokoena",
                    Email = "lerato@example.com",
                    Phone = "071 555 0101",
                    Skills = "Driving, Logistics & Distribution",
                    Availability = "Full-time",
                    Notes = "Code 10 driver's licence, can operate a delivery van.",
                    Status = "Pending"
                });
            await db.SaveChangesAsync();
        }

        // --- A couple of field updates so the pages don't look empty ----------
        if (!await db.ProjectUpdates.AnyAsync())
        {
            var projects = await db.ReliefProjects.OrderBy(p => p.Id).ToListAsync();
            if (projects.Count >= 2)
            {
                db.ProjectUpdates.AddRange(
                    new ProjectUpdate
                    {
                        ReliefProjectId = projects[0].Id,
                        Title = "2 000 food parcels delivered",
                        Body = "Our team completed the first distribution round in Umlazi today. Two thousand food parcels reached affected families.",
                        PostedByName = "staff@gotg.org",
                        PostedAt = DateTime.Now.AddDays(-2)
                    },
                    new ProjectUpdate
                    {
                        ReliefProjectId = projects[1].Id,
                        Title = "Blankets and warm meals at shelters",
                        Body = "Three shelters in Johannesburg received warm clothing, blankets and cooked meals as the cold front moved in.",
                        PostedByName = "staff@gotg.org",
                        PostedAt = DateTime.Now.AddDays(-1)
                    });
                await db.SaveChangesAsync();
            }
        }
    }

    private static async Task CreateDemoUserAsync(
        UserManager<IdentityUser> userManager, string email, string password, string role)
    {
        if (await userManager.FindByEmailAsync(email) is not null)
            return;

        var user = new IdentityUser { UserName = email, Email = email, EmailConfirmed = true };
        var result = await userManager.CreateAsync(user, password);
        if (result.Succeeded)
        {
            await userManager.AddToRoleAsync(user, role);
        }
    }
}
