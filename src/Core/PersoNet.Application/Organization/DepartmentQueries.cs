using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Organization;
using PersoNet.Domain.Organization;

namespace PersoNet.Application.Organization;

public sealed class DepartmentQueries(IAppDbContext db)
{
    /// <summary>
    /// All departments of the current company as a flat list; clients build the tree from <c>ParentId</c>.
    /// </summary>
    public async Task<IReadOnlyList<DepartmentResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var departments = db.Departments.AsNoTracking();

        if (!includeInactive)
        {
            departments = departments.Where(department => department.IsActive);
        }

        return await departments
            .OrderBy(department => department.Name)
            .Select(department => ToResponse(department))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<DepartmentResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var department = await db.Departments.AsNoTracking()
            .Where(department => department.Id == id)
            .Select(department => ToResponse(department))
            .FirstOrDefaultAsync(cancellationToken);

        return department is null ? OrganizationErrors.DepartmentNotFound : department;
    }

    internal static DepartmentResponse ToResponse(Department department) =>
        new(department.Id, department.Name, department.Code, department.ParentId, department.IsActive);
}
