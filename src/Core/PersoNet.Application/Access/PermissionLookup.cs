using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;

namespace PersoNet.Application.Access;

/// <summary>
/// The permissions a person has through the roles the company gave them. The person is the current employee whose work
/// e-mail is the account's e-mail, the same rule the portal uses; someone who has left has none.
/// </summary>
public sealed class PermissionLookup(IAppDbContext db, TimeProvider time)
{
    public async Task<IReadOnlySet<string>> ForEmailAsync(string? email, CancellationToken cancellationToken)
    {
        var normalized = email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return new HashSet<string>();
        }

        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        var employees = db.Employees
            .Where(employee => employee.Email == normalized && employee.HireDate <= today
                && (employee.TerminationDate == null || employee.TerminationDate >= today))
            .Select(employee => employee.Id);
        var roleIds = db.RoleAssignments.Where(assignment => employees.Contains(assignment.EmployeeId)).Select(assignment => assignment.RoleId);

        var granted = await db.Roles.AsNoTracking()
            .Where(role => roleIds.Contains(role.Id))
            .Select(role => role.Permissions)
            .ToListAsync(cancellationToken);

        // Codes a role still holds but the application no longer grants through roles are ignored.
        return granted.SelectMany(codes => codes).Where(Permissions.Assignable.Contains).ToHashSet(StringComparer.Ordinal);
    }
}
