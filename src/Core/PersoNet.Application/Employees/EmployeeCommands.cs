using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Employees;

namespace PersoNet.Application.Employees;

public sealed class EmployeeCommands(IAppDbContext db, ITenantContext tenant)
{
    public async Task<Result<EmployeeResponse>> CreateAsync(SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return EmployeeErrors.NoTenant;
        }

        Employee employee;
        try
        {
            employee = Employee.Create(tenantId, ToDetails(request));
        }
        catch (DomainException exception)
        {
            return EmployeeErrors.Invalid(exception);
        }

        if (await CheckAsync(employee, cancellationToken) is { } error)
        {
            return error;
        }

        db.Employees.Add(employee);
        await db.SaveChangesAsync(cancellationToken);

        return EmployeeQueries.ToResponse(employee);
    }

    public async Task<Result<EmployeeResponse>> UpdateAsync(Guid id, SaveEmployeeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await db.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        if (employee.TerminationDate is { } lastDay && request.HireDate > lastDay)
        {
            return EmployeeErrors.HireAfterTermination;
        }

        try
        {
            employee.Update(ToDetails(request));
        }
        catch (DomainException exception)
        {
            return EmployeeErrors.Invalid(exception);
        }

        if (await CheckAsync(employee, cancellationToken) is { } error)
        {
            return error;
        }

        await db.SaveChangesAsync(cancellationToken);

        return EmployeeQueries.ToResponse(employee);
    }

    public async Task<Result<EmployeeResponse>> TerminateAsync(Guid id, TerminateEmployeeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await db.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        if (request.LastDay < employee.HireDate)
        {
            return EmployeeErrors.TerminationBeforeHire;
        }

        employee.Terminate(request.LastDay);

        // The contracts end with the employment; ones that start later are left for HR to remove.
        var contracts = await db.Contracts.Where(contract => contract.EmployeeId == id).ToListAsync(cancellationToken);
        foreach (var contract in contracts.Where(contract => contract.StartDate <= request.LastDay))
        {
            contract.EndEarly(request.LastDay);
        }

        await db.SaveChangesAsync(cancellationToken);

        return EmployeeQueries.ToResponse(employee);
    }

    public async Task<Result<EmployeeResponse>> ReinstateAsync(Guid id, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        // The contracts ended together with the employment run again, unless a newer contract has taken their place.
        if (employee.TerminationDate is { } lastDay)
        {
            var contracts = await db.Contracts.Where(contract => contract.EmployeeId == id).ToListAsync(cancellationToken);
            var ended = contracts.Where(contract => contract.TerminatedOn == lastDay).ToList();
            foreach (var contract in ended)
            {
                contract.CancelEarlyEnd();
            }

            var others = contracts.Except(ended).Where(contract => contract.IsEmployment).ToList();
            if (ended.Any(contract => contract.IsEmployment && others.Any(other => other.Overlaps(contract.StartDate, contract.LastDay))))
            {
                return EmployeeErrors.ReinstateOverlap;
            }
        }

        employee.Reinstate();
        await db.SaveChangesAsync(cancellationToken);

        return EmployeeQueries.ToResponse(employee);
    }

    public async Task<Result<EmployeeResponse>> RehireAsync(Guid id, RehireEmployeeRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await db.Employees.FirstOrDefaultAsync(employee => employee.Id == id, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        if (employee.TerminationDate is not { } lastDay)
        {
            return EmployeeErrors.NotTerminated;
        }

        if (request.HireDate <= lastDay)
        {
            return EmployeeErrors.RehireBeforeTermination;
        }

        employee.Rehire(request.HireDate);
        await db.SaveChangesAsync(cancellationToken);

        return EmployeeQueries.ToResponse(employee);
    }

    private static EmployeeDetails ToDetails(SaveEmployeeRequest request) =>
        new(
            request.Number,
            request.FirstName,
            request.LastName,
            request.Email,
            request.Phone,
            request.HireDate,
            request.DepartmentId,
            request.PositionId,
            request.ManagerId);

    // Rules that need other rows: unique number and e-mail, existing references, no manager cycles.
    private async Task<AppError?> CheckAsync(Employee employee, CancellationToken cancellationToken)
    {
        if (await db.Employees.AnyAsync(other => other.Number == employee.Number && other.Id != employee.Id, cancellationToken))
        {
            return EmployeeErrors.NumberTaken;
        }

        if (employee.Email is { } email
            && await db.Employees.AnyAsync(other => other.Email == email && other.Id != employee.Id, cancellationToken))
        {
            return EmployeeErrors.EmailTaken;
        }

        if (employee.DepartmentId is { } departmentId
            && !await db.Departments.AnyAsync(department => department.Id == departmentId, cancellationToken))
        {
            return EmployeeErrors.DepartmentNotFound;
        }

        if (employee.PositionId is { } positionId
            && !await db.Positions.AnyAsync(position => position.Id == positionId, cancellationToken))
        {
            return EmployeeErrors.PositionNotFound;
        }

        if (employee.ManagerId is { } managerId)
        {
            var managers = await db.Employees.AsNoTracking()
                .Where(other => other.Id != employee.Id)
                .ToDictionaryAsync(other => other.Id, other => other.ManagerId, cancellationToken);

            if (!managers.ContainsKey(managerId))
            {
                return EmployeeErrors.ManagerNotFound;
            }

            if (ReportsTo(managerId, employee.Id, managers))
            {
                return EmployeeErrors.ManagerCycle;
            }
        }

        return null;
    }

    // Walks up the chain from the proposed manager; reaching the employee means they would manage themselves.
    private static bool ReportsTo(Guid candidate, Guid employee, Dictionary<Guid, Guid?> managers)
    {
        var visited = new HashSet<Guid>();
        Guid? current = candidate;

        while (current is { } node && visited.Add(node))
        {
            if (node == employee)
            {
                return true;
            }

            current = managers.GetValueOrDefault(node);
        }

        return false;
    }
}
