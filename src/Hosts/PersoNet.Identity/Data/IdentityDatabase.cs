using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PersoNet.Identity.Data;

public static class IdentityDatabase
{
    public const string ConnectionStringName = "personet";

    public static DbContextOptionsBuilder UseIdentityDatabase(this DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", IdentityDbContext.Schema))
            .UseSnakeCaseNamingConvention()
            .UseOpenIddict<Guid>();

        return options;
    }
}

/// <summary>
/// Used by "dotnet ef" to create migrations without starting the host.
/// </summary>
internal sealed class DesignTimeIdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__personet")
            ?? "Host=localhost;Database=personet;Username=postgres";

        var options = new DbContextOptionsBuilder<IdentityDbContext>();
        options.UseIdentityDatabase(connectionString);

        return new IdentityDbContext(options.Options);
    }
}
