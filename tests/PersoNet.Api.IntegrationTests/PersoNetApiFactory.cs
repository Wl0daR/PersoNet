using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using PersoNet.Testing;

namespace PersoNet.Api.IntegrationTests;

/// <summary>
/// Runs the API in Development against a fresh test database; startup applies the migrations.
/// </summary>
public sealed class PersoNetApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DatabaseName = "personet_api_tests";

    public async ValueTask InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionStringFor("postgres"));
        await connection.OpenAsync();

        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:personet", TestDatabase.ConnectionStringFor(DatabaseName));
    }
}
