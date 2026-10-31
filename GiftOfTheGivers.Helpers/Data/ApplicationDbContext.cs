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

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<StaffAuditEntry> StaffAuditEntries => Set<StaffAuditEntry>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ReliefProject>().Property(p => p.TargetAmount).HasPrecision(18, 2);
        builder.Entity<Donation>().HasIndex(d => d.SubmissionKey).IsUnique().HasFilter("[SubmissionKey] IS NOT NULL");
        builder.Entity<Donation>().HasOne(d => d.ReliefProject).WithMany().HasForeignKey(d => d.ReliefProjectId).OnDelete(DeleteBehavior.SetNull);
        builder.Entity<Notification>().HasIndex(n => new { n.UserId, n.ReadAtUtc });
        builder.Entity<Notification>().HasIndex(n => new { n.SentAtUtc, n.NextAttemptAtUtc });
        builder.Entity<StaffAuditEntry>().HasIndex(a => a.OccurredAtUtc);

        // SQL Server needs an explicit precision for money columns
        // (the default decimal(18,2) would otherwise warn about truncation).
        builder.Entity<Donation>()
            .Property(d => d.Amount)
            .HasPrecision(18, 2);
    }
}
