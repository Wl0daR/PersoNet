using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Organization;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Organization;

namespace PersoNet.Application.Organization;

public sealed class DepartmentCommands(IAppDbContext db, ITenantContext tenant)
{
    public async Task<Result<DepartmentResponse>> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return OrganizationErrors.NoTenant;
        }

        Department department;
        try
        {
            department = Department.Create(tenantId, request.Name, request.Code, request.ParentId);
        }
        catch (DomainException exception)
        {
            return OrganizationErrors.Invalid(exception);
        }

        if (await CodeTakenAsync(department.Code, exceptId: null, cancellationToken))
        {
            return OrganizationErrors.DepartmentCodeTaken;
        }

        if (request.ParentId is { } parentId && !await ExistsAsync(parentId, cancellationToken))
        {
            return OrganizationErrors.ParentNotFound;
        }

        db.Departments.Add(department);
        await db.SaveChangesAsync(cancellationToken);

        return DepartmentQueries.ToResponse(department);
    }

    public async Task<Result<DepartmentResponse>> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var department = await db.Departments.FirstOrDefaultAsync(department => department.Id == id, cancellationToken);
        if (department is null)
        {
            return OrganizationErrors.DepartmentNotFound;
        }

        try
        {
            department.Update(request.Name, request.Code);
            department.MoveTo(request.ParentId);
        }
        catch (DomainException exception)
        {
            return OrganizationErrors.Invalid(exception);
        }

        if (await CodeTakenAsync(department.Code, exceptId: id, cancellationToken))
        {
            return OrganizationErrors.DepartmentCodeTaken;
        }

        if (request.ParentId is { } parentId)
        {
            var parents = await db.Departments.AsNoTracking()
                .ToDictionaryAsync(other => other.Id, other => other.ParentId, cancellationToken);

            if (!parents.ContainsKey(parentId))
            {
                return OrganizationErrors.ParentNotFound;
            }

            if (IsDescendant(parentId, of: id, parents))
            {
                return OrganizationErrors.DepartmentCycle;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return DepartmentQueries.ToResponse(department);
    }

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var department = await db.Departments.FirstOrDefaultAsync(department => department.Id == id, cancellationToken);
        if (department is null)
        {
            return OrganizationErrors.DepartmentNotFound;
        }

        if (await db.Departments.AnyAsync(child => child.ParentId == id && child.IsActive, cancellationToken))
        {
            return OrganizationErrors.DepartmentHasActiveChildren;
        }

        department.Deactivate();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var department = await db.Departments.FirstOrDefaultAsync(department => department.Id == id, cancellationToken);
        if (department is null)
        {
            return OrganizationErrors.DepartmentNotFound;
        }

        department.Activate();
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    private Task<bool> CodeTakenAsync(string code, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Departments.AnyAsync(other => other.Code == code && other.Id != exceptId, cancellationToken);

    private Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken) =>
        db.Departments.AnyAsync(other => other.Id == id, cancellationToken);

    // Walks up from the candidate parent; reaching the department itself means the move would create a cycle.
    private static bool IsDescendant(Guid candidate, Guid of, Dictionary<Guid, Guid?> parents)
    {
        var visited = new HashSet<Guid>();
        Guid? current = candidate;

        while (current is { } node && visited.Add(node))
        {
            if (node == of)
            {
                return true;
            }

            current = parents.GetValueOrDefault(node);
        }

        return false;
    }
}
