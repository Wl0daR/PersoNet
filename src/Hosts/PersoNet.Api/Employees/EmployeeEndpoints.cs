using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Employees;
using PersoNet.Contracts.Employees;

namespace PersoNet.Api.Employees;

/// <summary>
/// Employee cards. Personal data, so reading needs <see cref="Permissions.EmployeesRead"/> and changes need
/// <see cref="Permissions.EmployeesManage"/>.
/// </summary>
internal static class EmployeeEndpoints
{
    public static RouteGroupBuilder MapEmployeeEndpoints(this RouteGroupBuilder api)
    {
        var employees = api.MapGroup("/employees").WithTags("Pracownicy");

        employees.MapGet("/", (
                    string? search,
                    Guid? departmentId,
                    bool? includeFormer,
                    int? page,
                    int? pageSize,
                    EmployeeQueries queries,
                    CancellationToken cancellationToken) =>
                queries.ListAsync(search, departmentId, includeFormer ?? false, page ?? 1, pageSize ?? EmployeeQueries.DefaultPageSize, cancellationToken))
            .RequireAuthorization(Permissions.EmployeesRead)
            .WithName("ListEmployees").WithSummary("Lista pracowników (stronicowana, z wyszukiwaniem)");

        employees.MapGet("/lookup", (EmployeeQueries queries, CancellationToken cancellationToken) =>
                queries.LookupAsync(cancellationToken))
            .RequireAuthorization(Permissions.EmployeesRead)
            .WithName("LookupEmployees").WithSummary("Obecni pracownicy do list wyboru");

        employees.MapGet("/{id:guid}", async (Guid id, EmployeeQueries queries, CancellationToken cancellationToken) =>
                (await queries.GetAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.EmployeesRead)
            .WithName("GetEmployee").WithSummary("Kartoteka pracownika");

        employees.MapPost("/", async (SaveEmployeeRequest request, EmployeeCommands commands, CancellationToken cancellationToken) =>
            {
                var result = await commands.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/employees/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Permissions.EmployeesManage)
            .WithName("CreateEmployee").WithSummary("Dodaj pracownika");

        employees.MapPut("/{id:guid}", async (Guid id, SaveEmployeeRequest request, EmployeeCommands commands, CancellationToken cancellationToken) =>
                (await commands.UpdateAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.EmployeesManage)
            .WithName("UpdateEmployee").WithSummary("Zmień dane pracownika");

        employees.MapPost("/{id:guid}/terminate", async (Guid id, TerminateEmployeeRequest request, EmployeeCommands commands, CancellationToken cancellationToken) =>
                (await commands.TerminateAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.EmployeesManage)
            .WithName("TerminateEmployee").WithSummary("Zakończ zatrudnienie (ostatni dzień pracy)");

        employees.MapPost("/{id:guid}/reinstate", async (Guid id, EmployeeCommands commands, CancellationToken cancellationToken) =>
                (await commands.ReinstateAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.EmployeesManage)
            .WithName("ReinstateEmployee").WithSummary("Cofnij zakończenie zatrudnienia");

        return api;
    }
}
