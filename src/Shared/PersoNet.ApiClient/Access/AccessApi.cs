using System.Net.Http.Json;
using PersoNet.Contracts.Access;
using PersoNet.Contracts.Account;

namespace PersoNet.ApiClient.Access;

internal sealed class AccessApi(HttpClient http) : IAccessApi
{
    private const string Roles = "api/v1/roles";

    public async Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri("api/v1/me", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<CurrentUserResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<PermissionResponse>> ListPermissionsAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri("api/v1/permissions", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<PermissionResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(Roles, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<RoleResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RoleResponse> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(Roles, request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<RoleResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<RoleResponse> UpdateRoleAsync(Guid id, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"{Roles}/{id}", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<RoleResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteRoleAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(new Uri($"{Roles}/{id}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoleResponse>> GetEmployeeRolesAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"api/v1/employees/{employeeId}/roles", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<RoleResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<RoleResponse>> SetEmployeeRolesAsync(Guid employeeId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"api/v1/employees/{employeeId}/roles", new SetEmployeeRolesRequest(roleIds), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<RoleResponse>>(response, cancellationToken).ConfigureAwait(false);
    }
}
