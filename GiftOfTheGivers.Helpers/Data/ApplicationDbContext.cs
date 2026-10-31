using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using GiftOfTheGivers.Helpers.Models;

namespace GiftOfTheGivers.Helpers.Data;

/// <summary>
/// EF Core database context. It inherits Identity's context so the ASP.NET
/// Identity tables (Users, Roles, UserRoles, ...) live in the same SQLite
/// database as the application's own tables.
/// </summary>
public class ApplicationDbContext : IdentityDbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Donation> Donations => Set<Donation>();
    public DbSet<Volunteer> Volunteers => Set<Volunteer>();
    public DbSet<ReliefProject> ReliefProjects => Set<ReliefProject>();
    public DbSet<ProjectUpdate> ProjectUpdates => Set<ProjectUpdate>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // SQL Server needs an explicit precision for money columns
        // (the default decimal(18,2) would otherwise warn about truncation).
        builder.Entity<Donation>()
            .Property(d => d.Amount)
            .HasPrecision(18, 2);
    }
}
