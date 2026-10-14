using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Audit;

namespace PersoNet.Api.Audit;

/// <summary>Who changed what and when, read from the audit log.</summary>
internal static class AuditEndpoints
{
    public static RouteGroupBuilder MapAuditEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/employees/{id:guid}/history", async (Guid id, AuditQueries audit, CancellationToken cancellationToken) =>
                (await audit.GetEmployeeHistoryAsync(id, cancellationToken)).ToOk())
            .WithTags("Audyt")
            .RequireAuthorization(Permissions.AuditRead)
            .WithName("GetEmployeeHistory")
            .WithSummary("Historia zmian kartoteki, umów, urlopów i konta pracownika (najnowsze najpierw)");

        return api;
    }
}
