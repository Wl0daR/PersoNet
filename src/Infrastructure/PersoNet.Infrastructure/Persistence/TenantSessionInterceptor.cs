using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace PersoNet.Infrastructure.Persistence;

/// <summary>
/// Tells PostgreSQL which company the connection works for, every time EF Core opens it.
/// Row-level security policies compare <c>tenant_id</c> with this setting (see docs/adr/0003),
/// so even SQL that bypasses the EF Core filters cannot read or write another company's rows.
/// </summary>
internal sealed class TenantSessionInterceptor(Func<Guid?> currentTenantId) : DbConnectionInterceptor
{
    public const string SettingName = "personet.tenant_id";

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        using var command = CreateCommand(connection);
        command.ExecuteNonQuery();
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await using var command = CreateCommand(connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // Always set, also to an empty value: pooled connections must not keep the previous user's company.
    private DbCommand CreateCommand(DbConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT set_config('{SettingName}', @tenant_id, false)";

        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant_id";
        parameter.Value = currentTenantId()?.ToString() ?? string.Empty;
        command.Parameters.Add(parameter);

        return command;
    }
}
