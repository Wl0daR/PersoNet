using Microsoft.EntityFrameworkCore;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Organization;
using PersoNet.Domain.Tenants;

namespace PersoNet.Application.Abstractions;

/// <summary>
/// The persistence entry point for use cases. The DbContext is the unit of work, so there is no repository layer on top.
/// </summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<Department> Departments { get; }

    DbSet<Position> Positions { get; }

    DbSet<Employee> Employees { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
