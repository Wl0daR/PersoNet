using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Organization;
using PersoNet.Domain.Organization;

namespace PersoNet.Application.Organization;

public sealed class PositionQueries(IAppDbContext db)
{
    public async Task<IReadOnlyList<PositionResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var positions = db.Positions.AsNoTracking();

        if (!includeInactive)
        {
            positions = positions.Where(position => position.IsActive);
        }

        return await positions
            .OrderBy(position => position.Name)
            .Select(position => ToResponse(position))
            .ToListAsync(cancellationToken);
    }

    public async Task<Result<PositionResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var position = await db.Positions.AsNoTracking()
            .Where(position => position.Id == id)
            .Select(position => ToResponse(position))
            .FirstOrDefaultAsync(cancellationToken);

        return position is null ? OrganizationErrors.PositionNotFound : position;
    }

    internal static PositionResponse ToResponse(Position position) => new(position.Id, position.Name, position.IsActive, position.DepartmentIds);
}
