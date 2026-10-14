using System.Net;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Access;
using PersoNet.Contracts.Access;
using PersoNet.Contracts.Account;
using PersoNet.Desktop.Presentation.Access;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>In-memory stand-in for the roles API with the server rules the screens rely on.</summary>
internal sealed class FakeAccessApi : IAccessApi
{
    public static readonly IReadOnlyList<PermissionResponse> Catalog =
    [
        new("employees.read", "Pracownicy", "Przeglądanie kartotek", "Lista pracowników.", true),
        new("leave.read", "Urlopy", "Przeglądanie urlopów", "Wnioski i saldo.", true),
        new("leave.manage", "Urlopy", "Decyzje urlopowe", "Zatwierdzanie wniosków.", true),
        new("accounts.manage", "Firma", "Konta w portalu", "Tylko administrator.", false),
    ];

    /// <summary>What the signed-in user may do; everything unless a test says otherwise.</summary>
    public List<string> Permissions { get; } = [.. UserAccessFor.Everything];

    public List<RoleResponse> Roles { get; } = [];

    /// <summary>Role ids by employee id.</summary>
    public Dictionary<Guid, HashSet<Guid>> Assignments { get; } = [];

    public Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new CurrentUserResponse(Guid.NewGuid(), Guid.NewGuid(), "Anna Kowalska", "anna@acme.test", Permissions));

    public Task<IReadOnlyList<PermissionResponse>> ListPermissionsAsync(CancellationToken cancellationToken) => Task.FromResult(Catalog);

    public Task<IReadOnlyList<RoleResponse>> ListRolesAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RoleResponse>>(Roles.Select(WithMembers).OrderBy(role => role.Name).ToList());

    public RoleResponse Add(string name, params string[] permissions)
    {
        var role = new RoleResponse(Guid.NewGuid(), name, null, permissions, 0);
        Roles.Add(role);
        return role;
    }

    public Task<RoleResponse> CreateRoleAsync(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        EnsureUniqueName(request.Name, exceptId: null);
        var role = new RoleResponse(Guid.NewGuid(), request.Name.Trim(), request.Description, [.. request.Permissions.Order(StringComparer.Ordinal)], 0);
        Roles.Add(role);
        return Task.FromResult(role);
    }

    public Task<RoleResponse> UpdateRoleAsync(Guid id, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        EnsureUniqueName(request.Name, id);
        var index = Roles.FindIndex(role => role.Id == id);
        Roles[index] = Roles[index] with { Name = request.Name.Trim(), Description = request.Description, Permissions = [.. request.Permissions.Order(StringComparer.Ordinal)] };
        return Task.FromResult(WithMembers(Roles[index]));
    }

    public Task DeleteRoleAsync(Guid id, CancellationToken cancellationToken)
    {
        if (Assignments.Values.Any(roles => roles.Contains(id)))
        {
            throw new ApiException(HttpStatusCode.Conflict, "Tę rolę mają pracownicy.", "role.in_use");
        }

        Roles.RemoveAll(role => role.Id == id);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RoleResponse>> GetEmployeeRolesAsync(Guid employeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<RoleResponse>>(RolesOf(employeeId));

    public Task<IReadOnlyList<RoleResponse>> SetEmployeeRolesAsync(Guid employeeId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken)
    {
        Assignments[employeeId] = [.. roleIds];
        return Task.FromResult<IReadOnlyList<RoleResponse>>(RolesOf(employeeId));
    }

    private List<RoleResponse> RolesOf(Guid employeeId) =>
        Roles.Where(role => Assignments.GetValueOrDefault(employeeId)?.Contains(role.Id) == true).Select(WithMembers).ToList();

    private RoleResponse WithMembers(RoleResponse role) => role with { MemberCount = Assignments.Values.Count(roles => roles.Contains(role.Id)) };

    private void EnsureUniqueName(string name, Guid? exceptId)
    {
        if (Roles.Any(role => role.Id != exceptId && string.Equals(role.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiException(HttpStatusCode.Conflict, "Rola o tej nazwie już istnieje.", "role.name_taken");
        }
    }
}

/// <summary>Signed-in users with given permissions.</summary>
internal static class UserAccessFor
{
    public static readonly string[] Everything =
    [
        UserAccess.EmployeesRead, UserAccess.EmployeesManage, UserAccess.LeaveRead, UserAccess.LeaveManage,
        UserAccess.OrganizationManage, UserAccess.AccountsManage, UserAccess.AuditRead, UserAccess.RolesManage, UserAccess.SettingsManage,
    ];

    public static UserAccess Administrator() => With(Everything);

    public static UserAccess With(params string[] permissions)
    {
        var access = new UserAccess();
        access.Set(permissions);
        return access;
    }
}
