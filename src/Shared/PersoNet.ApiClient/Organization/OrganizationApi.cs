using System.Net.Http.Json;
using PersoNet.Contracts.Organization;

namespace PersoNet.ApiClient.Organization;

internal sealed class OrganizationApi(HttpClient http) : IOrganizationApi
{
    private const string Departments = "api/v1/organization/departments";
    private const string Positions = "api/v1/organization/positions";

    public async Task<IReadOnlyList<DepartmentResponse>> GetDepartmentsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"{Departments}?includeInactive={Bool(includeInactive)}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<DepartmentResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(Departments, request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<DepartmentResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<DepartmentResponse> UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"{Departments}/{id}", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<DepartmentResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetDepartmentActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(new Uri($"{Departments}/{id}/{Action(active)}", UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"{Positions}?includeInactive={Bool(includeInactive)}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<PositionResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PositionResponse> CreatePositionAsync(CreatePositionRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(Positions, request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<PositionResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PositionResponse> UpdatePositionAsync(Guid id, UpdatePositionRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"{Positions}/{id}", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<PositionResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetPositionActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(new Uri($"{Positions}/{id}/{Action(active)}", UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    private static string Bool(bool value) => value ? "true" : "false";

    private static string Action(bool active) => active ? "activate" : "deactivate";
}
