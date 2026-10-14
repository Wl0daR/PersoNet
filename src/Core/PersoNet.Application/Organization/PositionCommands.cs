using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Organization;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Organization;

namespace PersoNet.Application.Organization;

public sealed class PositionCommands(IAppDbContext db, ITenantContext tenant)
{
    public async Task<Result<PositionResponse>> CreateAsync(CreatePositionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return OrganizationErrors.NoTenant;
        }

        if (!await DepartmentsExistAsync(request.DepartmentIds, cancellationToken))
        {
            return OrganizationErrors.PositionDepartmentNotFound;
        }

        Position position;
        try
        {
            position = Position.Create(tenantId, request.Name, request.DepartmentIds);
        }
        catch (DomainException exception)
        {
            return OrganizationErrors.Invalid(exception);
        }

        if (await NameTakenAsync(position.NormalizedName, exceptId: null, cancellationToken))
        {
            return OrganizationErrors.PositionNameTaken;
        }

        db.Positions.Add(position);
        await db.SaveChangesAsync(cancellationToken);

        return PositionQueries.ToResponse(position);
    }

    public async Task<Result<PositionResponse>> UpdateAsync(Guid id, UpdatePositionRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var position = await db.Positions.FirstOrDefaultAsync(position => position.Id == id, cancellationToken);
        if (position is null)
        {
            return OrganizationErrors.PositionNotFound;
        }

        if (!await DepartmentsExistAsync(request.DepartmentIds, cancellationToken))
        {
            return OrganizationErrors.PositionDepartmentNotFound;
        }

        try
        {
            position.Rename(request.Name);
            if (request.DepartmentIds is { } departmentIds)
            {
                position.AssignDepartments(departmentIds);
            }
        }
        catch (DomainException exception)
        {
            return OrganizationErrors.Invalid(exception);
        }

        if (await NameTakenAsync(position.NormalizedName, exceptId: id, cancellationToken))
        {
            return OrganizationErrors.PositionNameTaken;
        }

        await db.SaveChangesAsync(cancellationToken);

        return PositionQueries.ToResponse(position);
    }

    public Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: false, cancellationToken);

    public Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, active: true, cancellationToken);

    private async Task<Result> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        var position = await db.Positions.FirstOrDefaultAsync(position => position.Id == id, cancellationToken);
        if (position is null)
        {
            return OrganizationErrors.PositionNotFound;
        }

        if (active)
        {
            position.Activate();
        }
        else
        {
            position.Deactivate();
        }

        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    // Inactive departments still count: a position may stay tied to a department that is being phased out.
    private async Task<bool> DepartmentsExistAsync(IReadOnlyList<Guid>? departmentIds, CancellationToken cancellationToken)
    {
        if (departmentIds is not { Count: > 0 })
        {
            return true;
        }

        var wanted = departmentIds.Distinct().ToList();
        var found = await db.Departments.CountAsync(department => wanted.Contains(department.Id), cancellationToken);
        return found == wanted.Count;
    }

    private Task<bool> NameTakenAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
        db.Positions.AnyAsync(other => other.NormalizedName == normalizedName && other.Id != exceptId, cancellationToken);
}
