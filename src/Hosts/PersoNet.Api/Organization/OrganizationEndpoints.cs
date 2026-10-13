using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Organization;
using PersoNet.Contracts.Organization;

namespace PersoNet.Api.Organization;

/// <summary>
/// Company structure: departments and positions. Everyone in the company can read it; changes need
/// <see cref="Permissions.OrganizationManage"/>.
/// </summary>
internal static class OrganizationEndpoints
{
    public static RouteGroupBuilder MapOrganizationEndpoints(this RouteGroupBuilder api)
    {
        var departments = api.MapGroup("/organization/departments").WithTags("Struktura firmy");

        departments.MapGet("/", (bool? includeInactive, DepartmentQueries queries, CancellationToken cancellationToken) =>
                queries.ListAsync(includeInactive ?? false, cancellationToken))
            .WithName("ListDepartments").WithSummary("Lista działów");

        departments.MapGet("/{id:guid}", async (Guid id, DepartmentQueries queries, CancellationToken cancellationToken) =>
                (await queries.GetAsync(id, cancellationToken)).ToOk())
            .WithName("GetDepartment").WithSummary("Dział");

        departments.MapPost("/", async (CreateDepartmentRequest request, DepartmentCommands commands, CancellationToken cancellationToken) =>
            {
                var result = await commands.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/organization/departments/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Permissions.OrganizationManage)
            .WithName("CreateDepartment").WithSummary("Dodaj dział");

        departments.MapPut("/{id:guid}", async (Guid id, UpdateDepartmentRequest request, DepartmentCommands commands, CancellationToken cancellationToken) =>
                (await commands.UpdateAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.OrganizationManage)
            .WithName("UpdateDepartment").WithSummary("Zmień dział");

        departments.MapPost("/{id:guid}/deactivate", async (Guid id, DepartmentCommands commands, CancellationToken cancellationToken) =>
                (await commands.DeactivateAsync(id, cancellationToken)).ToNoContent())
            .RequireAuthorization(Permissions.OrganizationManage)
            .WithName("DeactivateDepartment").WithSummary("Dezaktywuj dział");

        departments.MapPost("/{id:guid}/activate", async (Guid id, DepartmentCommands commands, CancellationToken cancellationToken) =>
                (await commands.ActivateAsync(id, cancellationToken)).ToNoContent())
            .RequireAuthorization(Permissions.OrganizationManage)
            .WithName("ActivateDepartment").WithSummary("Aktywuj dział");

        var positions = api.MapGroup("/organization/positions").WithTags("Struktura firmy");

        positions.MapGet("/", (bool? includeInactive, PositionQueries queries, CancellationToken cancellationToken) =>
                queries.ListAsync(includeInactive ?? false, cancellationToken))
            .WithName("ListPositions").WithSummary("Lista stanowisk");

        positions.MapGet("/{id:guid}", async (Guid id, PositionQueries queries, CancellationToken cancellationToken) =>
                (await queries.GetAsync(id, cancellationToken)).ToOk())
            .WithName("GetPosition").WithSummary("Stanowisko");

        positions.MapPost("/", async (CreatePositionRequest request, PositionCommands commands, CancellationToken cancellationToken) =>
            {
                var result = await commands.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/organization/positions/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Permissions.OrganizationManage)
            .WithName("CreatePosition").WithSummary("Dodaj stanowisko");

        positions.MapPut("/{id:guid}", async (Guid id, UpdatePositionRequest request, PositionCommands commands, CancellationToken cancellationToken) =>
                (await commands.UpdateAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.OrganizationManage)
            .WithName("UpdatePosition").WithSummary("Zmień nazwę stanowiska");

        positions.MapPost("/{id:guid}/deactivate", async (Guid id, PositionCommands commands, CancellationToken cancellationToken) =>
                (await commands.DeactivateAsync(id, cancellationToken)).ToNoContent())
            .RequireAuthorization(Permissions.OrganizationManage)
            .WithName("DeactivatePosition").WithSummary("Dezaktywuj stanowisko");

        positions.MapPost("/{id:guid}/activate", async (Guid id, PositionCommands commands, CancellationToken cancellationToken) =>
                (await commands.ActivateAsync(id, cancellationToken)).ToNoContent())
            .RequireAuthorization(Permissions.OrganizationManage)
            .WithName("ActivatePosition").WithSummary("Aktywuj stanowisko");

        return api;
    }
}
