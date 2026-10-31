using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GiftOfTheGivers.Helpers.Data;

/// <summary>
/// Used ONLY by EF Core design-time tooling (dotnet ef / Add-Migration),
/// so migrations can be generated without starting the web app or
/// connecting to a real database. The connection string here is just a
/// placeholder — generating a migration never opens a connection.
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlServer(
                "Server=localhost;Database=GiftOfTheGiversDb;User Id=sa;Password=DesignTimePlaceholder!;TrustServerCertificate=True")
            .Options;
        return new ApplicationDbContext(options);
    }
}
