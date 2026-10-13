using Microsoft.EntityFrameworkCore.Migrations;

namespace PersoNet.Infrastructure.Persistence;

/// <summary>
/// Migration helpers for PostgreSQL row-level security on tenant-owned tables (see docs/adr/0003).
/// Every migration that creates a tenant-owned table calls <see cref="EnableTenantIsolation"/> for it.
/// </summary>
internal static class RowLevelSecurity
{
    public const string PolicyName = "tenant_isolation";

    /// <summary>
    /// Rows are visible and writable only when their <c>tenant_id</c> equals the connection's
    /// <c>personet.tenant_id</c> setting. No setting means no rows. FORCE applies the policy to the table owner too;
    /// only roles with BYPASSRLS (superusers) skip it, so the applications must connect as an ordinary role.
    /// </summary>
    public static void EnableTenantIsolation(this MigrationBuilder migrationBuilder, string table)
    {
        ArgumentNullException.ThrowIfNull(migrationBuilder);

        var tenantMatches =
            $"tenant_id = NULLIF(current_setting('{TenantSessionInterceptor.SettingName}', true), '')::uuid";

        migrationBuilder.Sql($"ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"ALTER TABLE {table} FORCE ROW LEVEL SECURITY;");
        migrationBuilder.Sql($"CREATE POLICY {PolicyName} ON {table} USING ({tenantMatches}) WITH CHECK ({tenantMatches});");
    }
}
