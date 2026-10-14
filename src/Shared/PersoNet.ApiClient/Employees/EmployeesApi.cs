using System.Globalization;
using System.Net.Http.Json;
using PersoNet.Contracts.Account;
using PersoNet.Contracts.Audit;
using PersoNet.Contracts.Common;
using PersoNet.Contracts.Employees;

namespace PersoNet.ApiClient.Employees;

internal sealed class EmployeesApi(HttpClient http) : IEmployeesApi
{
    private const string Employees = "api/v1/employees";

    public async Task<PagedResponse<EmployeeListItem>> ListAsync(EmployeeListQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var parameters = new List<string>
        {
            $"includeFormer={(query.IncludeFormer ? "true" : "false")}",
            "page=" + query.Page.ToString(CultureInfo.InvariantCulture),
            "pageSize=" + query.PageSize.ToString(CultureInfo.InvariantCulture),
        };

        if (query.Sort != EmployeeSort.Name || query.Descending)
        {
            parameters.Add("sort=" + query.Sort);
            parameters.Add($"descending={(query.Descending ? "true" : "false")}");
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parameters.Add("search=" + Uri.EscapeDataString(query.Search.Trim()));
        }

        if (query.DepartmentId is { } departmentId)
        {
            parameters.Add($"departmentId={departmentId}");
        }

        using var response = await http.GetAsync(new Uri($"{Employees}?{string.Join('&', parameters)}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<PagedResponse<EmployeeListItem>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<EmployeeLookupItem>> LookupAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"{Employees}/lookup", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<EmployeeLookupItem>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"{Employees}/{id}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeResponse> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(Employees, request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImportEmployeesResponse> ImportAsync(ImportEmployeesRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"{Employees}/import", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<ImportEmployeesResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeResponse> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"{Employees}/{id}", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeResponse> TerminateAsync(Guid id, DateOnly lastDay, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"{Employees}/{id}/terminate", new TerminateEmployeeRequest(lastDay), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeResponse> RehireAsync(Guid id, DateOnly hireDate, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"{Employees}/{id}/rehire", new RehireEmployeeRequest(hireDate), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeAccountResponse> GetAccountAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"{Employees}/{employeeId}/account", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeAccountResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AccountLinkResponse> CreateAccountLinkAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(new Uri($"{Employees}/{employeeId}/account/link", UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<AccountLinkResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeAccountResponse> SetAccountBlockedAsync(Guid employeeId, bool blocked, CancellationToken cancellationToken)
    {
        var action = blocked ? "block" : "unblock";
        using var response = await http.PostAsync(new Uri($"{Employees}/{employeeId}/account/{action}", UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeAccountResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<HistoryEntryResponse>> GetHistoryAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"{Employees}/{employeeId}/history", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<HistoryEntryResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeResponse> ReinstateAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(new Uri($"{Employees}/{id}/reinstate", UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ContractResponse>> ListContractsAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"{Employees}/{employeeId}/contracts", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<ContractResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ContractResponse> CreateContractAsync(Guid employeeId, SaveContractRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"{Employees}/{employeeId}/contracts", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<ContractResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ContractResponse> UpdateContractAsync(Guid employeeId, Guid contractId, SaveContractRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"{Employees}/{employeeId}/contracts/{contractId}", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<ContractResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteContractAsync(Guid employeeId, Guid contractId, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(new Uri($"{Employees}/{employeeId}/contracts/{contractId}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }
}
