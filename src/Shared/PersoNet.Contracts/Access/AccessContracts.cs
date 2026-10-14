namespace PersoNet.Contracts.Access;

/// <summary>
/// A permission as people read it. <see cref="Group"/> names the area ("Pracownicy"), and <see cref="Assignable"/> is false
/// for permissions only the HR administrator has.
/// </summary>
public sealed record PermissionResponse(string Code, string Group, string Name, string Description, bool Assignable);

/// <summary>A role of the company, with the permissions it grants and how many people have it.</summary>
public sealed record RoleResponse(Guid Id, string Name, string? Description, IReadOnlyList<string> Permissions, int MemberCount);

/// <summary>Create and update take the same fields.</summary>
public sealed record SaveRoleRequest(string Name, string? Description, IReadOnlyList<string> Permissions);

/// <summary>All roles the employee should have; roles left out are taken away.</summary>
public sealed record SetEmployeeRolesRequest(IReadOnlyList<Guid> RoleIds);
