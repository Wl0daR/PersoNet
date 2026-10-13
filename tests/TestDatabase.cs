using Npgsql;

namespace PersoNet.Testing;

/// <summary>
/// Connection details for integration tests: a real PostgreSQL, never Docker.
/// Locally: the installed PostgreSQL; set PERSONET_TEST_POSTGRES when it does not use postgres/postgres.
/// In CI: the postgres service of the GitHub Actions job.
/// </summary>
internal static class TestDatabase
{
    private const string EnvironmentVariable = "PERSONET_TEST_POSTGRES";
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Username=postgres;Password=postgres";

    /// <summary>
    /// Connection string to a database reserved for one test project. Each project uses its own database so they can run in parallel.
    /// </summary>
    public static string ConnectionStringFor(string database) =>
        new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(EnvironmentVariable) ?? DefaultConnectionString)
        {
            Database = database,
        }.ConnectionString;
}
