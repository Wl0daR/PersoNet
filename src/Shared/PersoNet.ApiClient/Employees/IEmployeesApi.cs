using PersoNet.Contracts.Common;
using PersoNet.Contracts.Employees;

namespace PersoNet.ApiClient.Employees;

/// <summary>Employee cards in PersoNet.Api. Failures throw <see cref="ApiException"/>.</summary>
public interface IEmployeesApi
{
    Task<PagedResponse<EmployeeListItem>> ListAsync(EmployeeListQuery query, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmployeeLookupItem>> LookupAsync(CancellationToken cancellationToken);

    Task<EmployeeResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<EmployeeResponse> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken);

    Task<EmployeeResponse> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken cancellationToken);

    Task<EmployeeResponse> TerminateAsync(Guid id, DateOnly lastDay, CancellationToken cancellationToken);

    Task<EmployeeResponse> ReinstateAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ContractResponse>> ListContractsAsync(Guid employeeId, CancellationToken cancellationToken);

    Task<ContractResponse> CreateContractAsync(Guid employeeId, SaveContractRequest request, CancellationToken cancellationToken);

    Task<ContractResponse> UpdateContractAsync(Guid employeeId, Guid contractId, SaveContractRequest request, CancellationToken cancellationToken);

    Task DeleteContractAsync(Guid employeeId, Guid contractId, CancellationToken cancellationToken);
}

public sealed record EmployeeListQuery(string? Search, Guid? DepartmentId, bool IncludeFormer, int Page, int PageSize);
