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
                    EmployeeSort? sort,
                    bool? descending,
                    EmployeeQueries queries,
                    CancellationToken cancellationToken) =>
                queries.ListAsync(
                    search, departmentId, includeFormer ?? false, page ?? 1, pageSize ?? EmployeeQueries.DefaultPageSize,
                    sort ?? EmployeeSort.Name, descending ?? false, cancellationToken))
            .RequireAuthorization(Permissions.EmployeesRead)
            .WithName("ListEmployees").WithSummary("Lista pracowników (stronicowana, z wyszukiwaniem i sortowaniem)");

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

        employees.MapPost("/{id:guid}/rehire", async (Guid id, RehireEmployeeRequest request, EmployeeCommands commands, CancellationToken cancellationToken) =>
                (await commands.RehireAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.EmployeesManage)
            .WithName("RehireEmployee").WithSummary("Zatrudnij ponownie byłego pracownika od podanej daty");

        var contracts = employees.MapGroup("/{employeeId:guid}/contracts").WithTags("Pracownicy");

        contracts.MapGet("/", async (Guid employeeId, ContractService service, CancellationToken cancellationToken) =>
                (await service.ListAsync(employeeId, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.EmployeesRead)
            .WithName("ListContracts").WithSummary("Umowy pracownika, od najnowszej");

        contracts.MapPost("/", async (Guid employeeId, SaveContractRequest request, ContractService service, CancellationToken cancellationToken) =>
            {
                var result = await service.CreateAsync(employeeId, request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/employees/{employeeId}/contracts/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Permissions.EmployeesManage)
            .WithName("CreateContract").WithSummary("Dodaj umowę");

        contracts.MapPut("/{contractId:guid}", async (Guid employeeId, Guid contractId, SaveContractRequest request, ContractService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(employeeId, contractId, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.EmployeesManage)
            .WithName("UpdateContract").WithSummary("Zmień umowę");

        contracts.MapDelete("/{contractId:guid}", async (Guid employeeId, Guid contractId, ContractService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(employeeId, contractId, cancellationToken)).ToNoContent())
            .RequireAuthorization(Permissions.EmployeesManage)
            .WithName("DeleteContract").WithSummary("Usuń umowę wprowadzoną omyłkowo");

        return api;
    }
}
