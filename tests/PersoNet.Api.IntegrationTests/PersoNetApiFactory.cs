using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Accounts;
using PersoNet.Domain.Tenants;
using PersoNet.Testing;

namespace PersoNet.Api.IntegrationTests;

/// <summary>
/// Runs the API in Development against a fresh test database; startup applies the migrations.
/// </summary>
public sealed class PersoNetApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string DatabaseName = "personet_api_tests";

    private WebApplicationFactory<Program>? _signedIn;

    /// <summary>The portal accounts the signed-in host sees instead of the login server.</summary>
    public FakeAccountDirectory Accounts { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(TestDatabase.ConnectionStringFor("postgres"));
        await connection.OpenAsync();

        await using var drop = new NpgsqlCommand($"DROP DATABASE IF EXISTS {DatabaseName} WITH (FORCE)", connection);
        await drop.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// A client signed in through <see cref="TestAuthHandler"/>.
    /// </summary>
    public HttpClient CreateSignedInClient(Guid? tenantId = null, params string[] roles)
    {
        // One signed-in host for all tests; the derived factory is disposed with this one.
        _signedIn ??= WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
            services.AddSingleton<IAccountDirectory>(Accounts);
        }));

        var client = _signedIn.CreateClient();

        if (tenantId is { } tenant)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.TenantHeader, tenant.ToString());
        }

        if (roles.Length > 0)
        {
            client.DefaultRequestHeaders.Add(TestAuthHandler.RolesHeader, string.Join(',', roles));
        }

        return client;
    }

    /// <summary>
    /// A client of an ordinary employee in the portal: an account with the given e-mail and no HR role.
    /// </summary>
    public HttpClient CreatePortalClient(string email)
    {
        var client = CreateSignedInClient(null, "employee");
        client.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);
        return client;
    }

    /// <summary>
    /// Adds another company, for tests that check one company cannot see another's data.
    /// </summary>
    public async Task<Guid> CreateTenantAsync()
    {
        var tenant = Tenant.Create("Inna firma", "inna-" + Guid.NewGuid().ToString("N")[..8]);

        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync();

        return tenant.Id;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:personet", TestDatabase.ConnectionStringFor(DatabaseName));

        // Never contacted: requests without a token are rejected before discovery, and other tests sign in with TestAuthHandler.
        builder.UseSetting("Identity:Authority", "https://identity.personet.test/");
    }
}

/// <summary>
/// All API tests share one host and one test database, so startup migrations and seeding run once.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ApiTestGroup : ICollectionFixture<PersoNetApiFactory>
{
    public const string Name = "Api";
}
