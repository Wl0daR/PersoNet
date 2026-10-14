using PersoNet.Contracts.Access;
using PersoNet.Contracts.Account;

namespace PersoNet.ApiClient.Access;

/// <summary>The signed-in user, and the company's roles and permissions. Failures throw <see cref="ApiException"/>.</summary>
public interface IAccessApi
{
    /// <summary>Who is signed in and what they may do.</summary>
    Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PermissionResponse>> ListPermissionsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken);

    Task<RoleResponse> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken);

    Task<RoleResponse> UpdateRoleAsync(Guid id, SaveRoleRequest request, CancellationToken cancellationToken);

    Task DeleteRoleAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleResponse>> GetEmployeeRolesAsync(Guid employeeId, CancellationToken cancellationToken);

    /// <summary>Gives the employee exactly these roles.</summary>
    Task<IReadOnlyList<RoleResponse>> SetEmployeeRolesAsync(Guid employeeId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken);
}
