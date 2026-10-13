using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PersoNet.Infrastructure.Tenancy;

namespace PersoNet.Infrastructure.Persistence;

/// <summary>
/// Used by "dotnet ef" to create migrations without starting a host. Adding a migration does not connect to the database.
/// </summary>
internal sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PersoNetDbContext>
{
    public PersoNetDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__personet")
            ?? "Host=localhost;Database=personet;Username=postgres";

        var options = new DbContextOptionsBuilder<PersoNetDbContext>();
        options.UsePersoNetDatabase(connectionString);

        return new PersoNetDbContext(options.Options, new NoTenantContext());
    }
}
