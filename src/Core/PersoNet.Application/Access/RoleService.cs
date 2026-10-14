using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Access;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Access;

namespace PersoNet.Application.Access;

/// <summary>Roles the company defines and the people who have them.</summary>
public sealed class RoleService(IAppDbContext db, ITenantContext tenant)
{
    public async Task<IReadOnlyList<RoleResponse>> ListAsync(CancellationToken cancellationToken)
    {
        var roles = await db.Roles.AsNoTracking().OrderBy(role => role.Name).ToListAsync(cancellationToken);
        var members = await MemberCountsAsync(cancellationToken);

        return roles.Select(role => ToResponse(role, members.GetValueOrDefault(role.Id))).ToList();
    }

    public async Task<Result<RoleResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await db.Roles.AsNoTracking().FirstOrDefaultAsync(role => role.Id == id, cancellationToken);
        return role is null ? AccessErrors.RoleNotFound : ToResponse(role, await MemberCountAsync(id, cancellationToken));
    }

    public async Task<Result<RoleResponse>> CreateAsync(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return AccessErrors.NoTenant;
        }

        if (Check(request) is { } error)
        {
            return error;
        }

        Role role;
        try
        {
            role = Role.Create(tenantId, request.Name, request.Description, request.Permissions);
        }
        catch (DomainException exception)
        {
            return AccessErrors.Invalid(exception);
        }

        if (await NameTakenAsync(role.NormalizedName, exceptId: null, cancellationToken))
        {
            return AccessErrors.NameTaken;
        }

        db.Roles.Add(role);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(role, 0);
    }

    public async Task<Result<RoleResponse>> UpdateAsync(Guid id, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var role = await db.Roles.FirstOrDefaultAsync(role => role.Id == id, cancellationToken);
        if (role is null)
        {
            return AccessErrors.RoleNotFound;
        }

        if (Check(request) is { } error)
        {
            return error;
        }

        try
        {
            role.Change(request.Name, request.Description, request.Permissions);
        }
        catch (DomainException exception)
        {
            return AccessErrors.Invalid(exception);
        }

        if (await NameTakenAsync(role.NormalizedName, exceptId: id, cancellationToken))
        {
            return AccessErrors.NameTaken;
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(role, await MemberCountAsync(id, cancellationToken));
    }

    /// <summary>Removes a role nobody has; one still given to people has to be taken from them first.</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var role = await db.Roles.FirstOrDefaultAsync(role => role.Id == id, cancellationToken);
        if (role is null)
        {
            return AccessErrors.RoleNotFound;
        }

        if (await db.RoleAssignments.AnyAsync(assignment => assignment.RoleId == id, cancellationToken))
        {
            return AccessErrors.InUse;
        }

        db.Roles.Remove(role);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    /// <summary>The roles the employee has, by name.</summary>
    public async Task<Result<IReadOnlyList<RoleResponse>>> GetEmployeeRolesAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return AccessErrors.EmployeeNotFound;
        }

        var roleIds = db.RoleAssignments.Where(assignment => assignment.EmployeeId == employeeId).Select(assignment => assignment.RoleId);
        var roles = await db.Roles.AsNoTracking().Where(role => roleIds.Contains(role.Id)).OrderBy(role => role.Name).ToListAsync(cancellationToken);
        var members = await MemberCountsAsync(cancellationToken);

        return roles.Select(role => ToResponse(role, members.GetValueOrDefault(role.Id))).ToList();
    }

    /// <summary>Gives the employee exactly these roles: new ones are added and the ones left out are taken away.</summary>
    public async Task<Result<IReadOnlyList<RoleResponse>>> SetEmployeeRolesAsync(Guid employeeId, SetEmployeeRolesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return AccessErrors.NoTenant;
        }

        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return AccessErrors.EmployeeNotFound;
        }

        var wanted = request.RoleIds.ToHashSet();
        if (await db.Roles.CountAsync(role => wanted.Contains(role.Id), cancellationToken) != wanted.Count)
        {
            return AccessErrors.UnknownRole;
        }

        var current = await db.RoleAssignments.Where(assignment => assignment.EmployeeId == employeeId).ToListAsync(cancellationToken);
        db.RoleAssignments.RemoveRange(current.Where(assignment => !wanted.Contains(assignment.RoleId)));
        db.RoleAssignments.AddRange(wanted
            .Where(roleId => current.All(assignment => assignment.RoleId != roleId))
            .Select(roleId => RoleAssignment.Create(tenantId, employeeId, roleId)));
        await db.SaveChangesAsync(cancellationToken);

        return await GetEmployeeRolesAsync(employeeId, cancellationToken);
    }

    private static AppError? Check(SaveRoleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return AccessErrors.NameRequired;
        }

        if (request.Name.Trim().Length > Role.NameMaxLength)
        {
            return AccessErrors.NameTooLong;
        }

        return request.Permissions.FirstOrDefault(code => !Permissions.Assignable.Contains(code)) is { } unknown
            ? AccessErrors.UnknownPermission(unknown)
            : null;
    }

    private Task<bool> NameTakenAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Roles.AnyAsync(other => other.NormalizedName == normalizedName && other.Id != exceptId, cancellationToken);

    private Task<Dictionary<Guid, int>> MemberCountsAsync(CancellationToken cancellationToken) =>
        db.RoleAssignments.GroupBy(assignment => assignment.RoleId)
            .Select(group => new { RoleId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.RoleId, group => group.Count, cancellationToken);

    private Task<int> MemberCountAsync(Guid roleId, CancellationToken cancellationToken) =>
        db.RoleAssignments.CountAsync(assignment => assignment.RoleId == roleId, cancellationToken);

    private static RoleResponse ToResponse(Role role, int members) => new(role.Id, role.Name, role.Description, role.Permissions, members);
}
