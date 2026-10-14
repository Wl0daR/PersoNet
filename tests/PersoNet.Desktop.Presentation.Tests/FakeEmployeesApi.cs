using System.Net;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.Contracts.Common;
using PersoNet.Contracts.Employees;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>
/// In-memory stand-in for the employee API with the server rules the screen relies on.
/// </summary>
internal sealed class FakeEmployeesApi : IEmployeesApi
{
    public List<EmployeeResponse> Employees { get; } = [];

    public List<EmployeeListQuery> Queries { get; } = [];

    public List<ContractResponse> Contracts { get; } = [];

    public Task<PagedResponse<EmployeeListItem>> ListAsync(EmployeeListQuery query, CancellationToken cancellationToken)
    {
        Queries.Add(query);

        var matching = Employees
            .Where(e => query.IncludeFormer || e.TerminationDate is null)
            .Where(e => string.IsNullOrWhiteSpace(query.Search) || $"{e.LastName} {e.FirstName} {e.Number}".Contains(query.Search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.LastName)
            .ToList();

        var items = matching
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(e => new EmployeeListItem(e.Id, e.Number, e.FirstName, e.LastName, e.Email, null, null, null, e.HireDate, e.TerminationDate))
            .ToList();

        return Task.FromResult(new PagedResponse<EmployeeListItem>(items, query.Page, query.PageSize, matching.Count));
    }

    public Task<IReadOnlyList<EmployeeLookupItem>> LookupAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<EmployeeLookupItem>>(Employees
            .Where(e => e.TerminationDate is null)
            .Select(e => new EmployeeLookupItem(e.Id, e.Number, $"{e.LastName} {e.FirstName}"))
            .ToList());

    public Task<EmployeeResponse> GetAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Employees.Single(e => e.Id == id));

    public Task<EmployeeResponse> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        EnsureNumberFree(request.Number, exceptId: null);

        var employee = new EmployeeResponse(
            Guid.CreateVersion7(), request.Number.ToUpperInvariant(), request.FirstName, request.LastName, request.Email, request.Phone,
            request.HireDate, null, request.DepartmentId, request.PositionId, request.ManagerId);
        Employees.Add(employee);
        return Task.FromResult(employee);
    }

    public Task<EmployeeResponse> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        EnsureNumberFree(request.Number, id);

        if (request.ManagerId == id)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Przełożonym nie może być ten pracownik ani nikt z jego podwładnych.", "employee.manager_cycle");
        }

        var index = Employees.FindIndex(e => e.Id == id);
        Employees[index] = Employees[index] with
        {
            Number = request.Number.ToUpperInvariant(),
            FirstName = request.FirstName,
            LastName = request.LastName,
            Email = request.Email,
            HireDate = request.HireDate,
            ManagerId = request.ManagerId,
        };
        return Task.FromResult(Employees[index]);
    }

    public Task<EmployeeResponse> TerminateAsync(Guid id, DateOnly lastDay, CancellationToken cancellationToken)
    {
        var index = Employees.FindIndex(e => e.Id == id);

        if (lastDay < Employees[index].HireDate)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Data zakończenia pracy nie może być wcześniejsza niż data zatrudnienia.", "employee.termination_before_hire");
        }

        Employees[index] = Employees[index] with { TerminationDate = lastDay };
        return Task.FromResult(Employees[index]);
    }

    public Task<EmployeeResponse> RehireAsync(Guid id, DateOnly hireDate, CancellationToken cancellationToken)
    {
        var index = Employees.FindIndex(e => e.Id == id);
        if (Employees[index].TerminationDate is not { } lastDay || hireDate <= lastDay)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Nowa data zatrudnienia musi być późniejsza niż ostatni dzień poprzedniej pracy.", "employee.rehire_before_termination");
        }

        Employees[index] = Employees[index] with { HireDate = hireDate, TerminationDate = null };
        return Task.FromResult(Employees[index]);
    }

    public Task<EmployeeResponse> ReinstateAsync(Guid id, CancellationToken cancellationToken)
    {
        var index = Employees.FindIndex(e => e.Id == id);
        Employees[index] = Employees[index] with { TerminationDate = null };
        return Task.FromResult(Employees[index]);
    }

    public Task<IReadOnlyList<ContractResponse>> ListContractsAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ContractResponse>>(Contracts.Where(c => c.EmployeeId == employeeId).OrderByDescending(c => c.StartDate).ToList());

    public Task<ContractResponse> CreateContractAsync(Guid employeeId, SaveContractRequest request, CancellationToken cancellationToken)
    {
        EnsureNoOverlap(employeeId, null, request);

        var contract = new ContractResponse(Guid.CreateVersion7(), employeeId, request.Type, request.StartDate, request.EndDate, request.WorkingTime);
        Contracts.Add(contract);
        return Task.FromResult(contract);
    }

    public Task<ContractResponse> UpdateContractAsync(Guid employeeId, Guid contractId, SaveContractRequest request, CancellationToken cancellationToken)
    {
        EnsureNoOverlap(employeeId, contractId, request);

        var index = Contracts.FindIndex(c => c.Id == contractId);
        Contracts[index] = Contracts[index] with { Type = request.Type, StartDate = request.StartDate, EndDate = request.EndDate, WorkingTime = request.WorkingTime };
        return Task.FromResult(Contracts[index]);
    }

    public Task DeleteContractAsync(Guid employeeId, Guid contractId, CancellationToken cancellationToken)
    {
        Contracts.RemoveAll(c => c.Id == contractId);
        return Task.CompletedTask;
    }

    public EmployeeResponse Add(string number, string firstName, string lastName, DateOnly? hireDate = null)
    {
        var employee = new EmployeeResponse(
            Guid.CreateVersion7(), number, firstName, lastName, null, null, hireDate ?? new DateOnly(2024, 1, 1), null, null, null, null);
        Employees.Add(employee);
        return employee;
    }

    private void EnsureNoOverlap(Guid employeeId, Guid? exceptId, SaveContractRequest request)
    {
        if (Contracts.Any(c => c.EmployeeId == employeeId && c.Id != exceptId
            && c.StartDate <= (request.EndDate ?? DateOnly.MaxValue) && request.StartDate <= (c.EndDate ?? DateOnly.MaxValue)))
        {
            throw new ApiException(HttpStatusCode.Conflict, "W tym okresie pracownik ma już inną umowę o pracę.", "contract.overlap");
        }
    }

    private void EnsureNumberFree(string number, Guid? exceptId)
    {
        if (Employees.Any(e => e.Id != exceptId && string.Equals(e.Number, number, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiException(HttpStatusCode.Conflict, "Pracownik o tym numerze ewidencyjnym już istnieje.", "employee.number_taken");
        }
    }
}
