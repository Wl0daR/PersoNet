using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Domain.Employees;

namespace PersoNet.Application.Employees;

public sealed class EmployeeQueries(IAppDbContext db, TimeProvider time)
{
    public const int DefaultPageSize = 50;
    public const int MaxPageSize = 200;

    /// <summary>
    /// One page of employees sorted by name. <paramref name="search"/> matches name, personnel number or e-mail;
    /// people who have already left are hidden unless <paramref name="includeFormer"/> is set.
    /// </summary>
    public async Task<PagedResponse<EmployeeListItem>> ListAsync(
        string? search, Guid? departmentId, bool includeFormer, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Max(page, 1);
        pageSize = pageSize <= 0 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var employees = db.Employees.AsNoTracking();

        if (!includeFormer)
        {
            var today = Today();
            employees = employees.Where(employee => employee.TerminationDate == null || employee.TerminationDate >= today);
        }

        if (departmentId is { } department)
        {
            employees = employees.Where(employee => employee.DepartmentId == department);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // SearchText is stored upper-case, so the match ignores letter case without database-specific functions.
            var term = search.Trim().ToUpperInvariant();
            employees = employees.Where(employee => employee.SearchText.Contains(term));
        }

        var total = await employees.CountAsync(cancellationToken);

        var items = await employees
            .OrderBy(employee => employee.LastName)
            .ThenBy(employee => employee.FirstName)
            .ThenBy(employee => employee.Number)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(employee => new EmployeeListItem(
                employee.Id,
                employee.Number,
                employee.FirstName,
                employee.LastName,
                employee.Email,
                db.Departments.Where(d => d.Id == employee.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                db.Positions.Where(p => p.Id == employee.PositionId).Select(p => p.Name).FirstOrDefault(),
                db.Employees.Where(m => m.Id == employee.ManagerId).Select(m => m.FirstName + " " + m.LastName).FirstOrDefault(),
                employee.HireDate,
                employee.TerminationDate))
            .ToListAsync(cancellationToken);

        return new PagedResponse<EmployeeListItem>(items, page, pageSize, total);
    }

    public async Task<Result<EmployeeResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking()
            .Where(employee => employee.Id == id)
            .Select(employee => ToResponse(employee))
            .FirstOrDefaultAsync(cancellationToken);

        return employee is null ? EmployeeErrors.NotFound : employee;
    }

    /// <summary>Current employees for pickers, such as choosing a manager.</summary>
    public async Task<IReadOnlyList<EmployeeLookupItem>> LookupAsync(CancellationToken cancellationToken)
    {
        var today = Today();

        return await db.Employees.AsNoTracking()
            .Where(employee => employee.TerminationDate == null || employee.TerminationDate >= today)
            .OrderBy(employee => employee.LastName)
            .ThenBy(employee => employee.FirstName)
            .Select(employee => new EmployeeLookupItem(employee.Id, employee.Number, employee.LastName + " " + employee.FirstName))
            .ToListAsync(cancellationToken);
    }

    internal static EmployeeResponse ToResponse(Employee employee) =>
        new(
            employee.Id,
            employee.Number,
            employee.FirstName,
            employee.LastName,
            employee.Email,
            employee.Phone,
            employee.HireDate,
            employee.TerminationDate,
            employee.DepartmentId,
            employee.PositionId,
            employee.ManagerId);

    private DateOnly Today() => DateOnly.FromDateTime(time.GetLocalNow().DateTime);
}
