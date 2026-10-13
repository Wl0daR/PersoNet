using Microsoft.EntityFrameworkCore;

namespace PersoNet.Infrastructure.Persistence;

/// <summary>
/// The one place that configures the provider, so the app, design-time tools and tests build the same model.
/// </summary>
public static class DbContextOptionsSetup
{
    public const string ConnectionStringName = "personet";

    public static DbContextOptionsBuilder UsePersoNetDatabase(this DbContextOptionsBuilder options, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options
            .UseNpgsql(connectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history"))
            .UseSnakeCaseNamingConvention();
    }
}
