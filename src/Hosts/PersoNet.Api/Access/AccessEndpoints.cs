using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Access;
using PersoNet.Contracts.Access;

namespace PersoNet.Api.Access;

/// <summary>Roles the company defines, what each permits, and who has them. All of it needs <see cref="Permissions.RolesManage"/>.</summary>
internal static class AccessEndpoints
{
    public static RouteGroupBuilder MapAccessEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/permissions", () => PermissionCatalog.All)
            .WithTags("Role i uprawnienia")
            .RequireAuthorization(Permissions.RolesManage)
            .WithName("ListPermissions").WithSummary("Uprawnienia, które można nadać w roli");

        var roles = api.MapGroup("/roles").WithTags("Role i uprawnienia").RequireAuthorization(Permissions.RolesManage);

        roles.MapGet("/", (RoleService roles, CancellationToken cancellationToken) => roles.ListAsync(cancellationToken))
            .WithName("ListRoles").WithSummary("Role firmy");

        roles.MapGet("/{id:guid}", async (Guid id, RoleService roles, CancellationToken cancellationToken) =>
                (await roles.GetAsync(id, cancellationToken)).ToOk())
            .WithName("GetRole").WithSummary("Rola");

        roles.MapPost("/", async (SaveRoleRequest request, RoleService roles, CancellationToken cancellationToken) =>
            {
                var result = await roles.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/roles/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .WithName("CreateRole").WithSummary("Dodaj rolę");

        roles.MapPut("/{id:guid}", async (Guid id, SaveRoleRequest request, RoleService roles, CancellationToken cancellationToken) =>
                (await roles.UpdateAsync(id, request, cancellationToken)).ToOk())
            .WithName("UpdateRole").WithSummary("Zmień rolę");

        roles.MapDelete("/{id:guid}", async (Guid id, RoleService roles, CancellationToken cancellationToken) =>
                (await roles.DeleteAsync(id, cancellationToken)).ToNoContent())
            .WithName("DeleteRole").WithSummary("Usuń rolę, której nikt nie ma");

        var employeeRoles = api.MapGroup("/employees/{id:guid}/roles").WithTags("Role i uprawnienia").RequireAuthorization(Permissions.RolesManage);

        employeeRoles.MapGet("/", async (Guid id, RoleService roles, CancellationToken cancellationToken) =>
                (await roles.GetEmployeeRolesAsync(id, cancellationToken)).ToOk())
            .WithName("GetEmployeeRoles").WithSummary("Role pracownika");

        employeeRoles.MapPut("/", async (Guid id, SetEmployeeRolesRequest request, RoleService roles, CancellationToken cancellationToken) =>
                (await roles.SetEmployeeRolesAsync(id, request, cancellationToken)).ToOk())
            .WithName("SetEmployeeRoles").WithSummary("Nadaj pracownikowi dokładnie te role");

        return api;
    }
}
