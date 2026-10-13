using Microsoft.EntityFrameworkCore;
using Npgsql;
using PersoNet.Application.Abstractions;
using PersoNet.Infrastructure.Persistence;
using PersoNet.Testing;

namespace PersoNet.Infrastructure.IntegrationTests;

/// <summary>
/// Proves the PostgreSQL row-level security policies from the migrations: with plain SQL, past the EF Core filters,
/// an ordinary database role still sees and writes only the company set on its connection.
/// </summary>
public sealed class RowLevelSecurityTests : IAsyncLifetime
{
    private const string DatabaseName = "personet_rls_tests";
    private const string AppRole = "personet_rls_test_app";
    private const string AppRolePassword = "rls-test-only";

    private static readonly string AdminConnectionString = TestDatabase.ConnectionStringFor(DatabaseName);

    private static readonly string AppConnectionString = new NpgsqlConnectionStringBuilder(AdminConnectionString)
    {
        Username = AppRole,
        Password = AppRolePassword,
    }.ConnectionString;

    private readonly Guid _tenantA = Guid.CreateVersion7();
    private readonly Guid _tenantB = Guid.CreateVersion7();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async ValueTask InitializeAsync()
    {
        // Superuser: fresh database with the real migrations, and an ordinary role like the one the apps use on the server.
        await using (var admin = CreateContext(AdminConnectionString, tenantId: null))
        {
            await admin.Database.EnsureDeletedAsync(Cancellation);
            await admin.Database.MigrateAsync(Cancellation);
        }

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync(Cancellation);

        await ExecuteAsync(connection, $"""
            DO $$
            BEGIN
                CREATE ROLE {AppRole} LOGIN PASSWORD '{AppRolePassword}' NOSUPERUSER NOBYPASSRLS;
            EXCEPTION WHEN duplicate_object THEN NULL;
            END $$;
            GRANT USAGE ON SCHEMA public TO {AppRole};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO {AppRole};
            """);

        // Superusers bypass row-level security, so seeding both companies here is allowed.
        await ExecuteAsync(connection, $"""
            INSERT INTO tenants (id, name, subdomain, is_active) VALUES
                ('{_tenantA}', 'Firma A', 'a-{_tenantA:N}', true),
                ('{_tenantB}', 'Firma B', 'b-{_tenantB:N}', true);
            INSERT INTO departments (id, tenant_id, name, code, parent_id, is_active) VALUES
                ('{Guid.CreateVersion7()}', '{_tenantA}', 'Dział A', 'A', NULL, true),
                ('{Guid.CreateVersion7()}', '{_tenantB}', 'Dział B', 'B', NULL, true);
            """);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Plain_sql_sees_only_the_connection_tenant_rows()
    {
        await using var context = CreateContext(AppConnectionString, _tenantA);

        var codes = await context.Database.SqlQueryRaw<string>("SELECT code AS \"Value\" FROM departments").ToListAsync(Cancellation);

        codes.ShouldBe(["A"]);
    }

    [Fact]
    public async Task Without_tenant_plain_sql_sees_no_rows()
    {
        await using var context = CreateContext(AppConnectionString, tenantId: null);

        var count = await context.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM departments").SingleAsync(Cancellation);

        count.ShouldBe(0);
    }

    [Fact]
    public async Task Plain_sql_cannot_insert_rows_of_another_tenant()
    {
        await using var context = CreateContext(AppConnectionString, _tenantA);

        var insert = () => context.Database.ExecuteSqlAsync(
            $"INSERT INTO departments (id, tenant_id, name, code, parent_id, is_active) VALUES ({Guid.CreateVersion7()}, {_tenantB}, 'Obcy', 'X', NULL, true)",
            Cancellation);

        var exception = await Should.ThrowAsync<PostgresException>(insert);
        exception.SqlState.ShouldBe(PostgresErrorCodes.InsufficientPrivilege);
    }

    [Fact]
    public async Task Every_table_with_tenant_data_has_row_level_security()
    {
        await using var context = CreateContext(AdminConnectionString, tenantId: null);

        var unprotected = await context.Database.SqlQueryRaw<string>("""
            SELECT c.relname AS "Value"
            FROM pg_class c
            JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relkind = 'r'
              AND EXISTS (SELECT 1 FROM pg_attribute a WHERE a.attrelid = c.oid AND a.attname = 'tenant_id' AND NOT a.attisdropped)
              AND NOT (c.relrowsecurity AND c.relforcerowsecurity)
            """).ToListAsync(Cancellation);

        unprotected.ShouldBeEmpty("Każda tabela z danymi firmy musi mieć EnableTenantIsolation w migracji.");
    }

    private static PersoNetDbContext CreateContext(string connectionString, Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<PersoNetDbContext>();
        options.UsePersoNetDatabase(connectionString);

        return new PersoNetDbContext(options.Options, new FixedTenantContext(tenantId));
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(Cancellation);
    }

    private sealed class FixedTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? TenantId { get; } = tenantId;
    }
}
