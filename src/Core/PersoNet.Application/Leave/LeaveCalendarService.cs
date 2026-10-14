using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Leave;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;

namespace PersoNet.Application.Leave;

/// <summary>
/// Who is on leave in a month, for planning: HR sees the whole company or a department (with the departments under it),
/// a manager sees their team. Cancelled and rejected requests are left out; waiting ones are shown, as they may still
/// happen.
/// </summary>
public sealed class LeaveCalendarService(IAppDbContext db)
{
    /// <summary>Everyone in the company, or in <paramref name="departmentId"/> and the departments under it.</summary>
    public async Task<Result<LeaveCalendarResponse>> GetForCompanyAsync(int year, int month, Guid? departmentId, CancellationToken cancellationToken)
    {
        if (Check(year, month) is { } error)
        {
            return error;
        }

        var employees = db.Employees.AsNoTracking();
        if (departmentId is { } rootId)
        {
            var departments = await db.Departments.AsNoTracking()
                .Select(department => new { department.Id, department.ParentId })
                .ToListAsync(cancellationToken);
            if (departments.All(department => department.Id != rootId))
            {
                return LeaveErrors.DepartmentNotFound;
            }

            // The department and every department below it.
            var included = new HashSet<Guid> { rootId };
            for (var added = true; added;)
            {
                added = false;
                foreach (var department in departments.Where(d => d.ParentId is { } parent && included.Contains(parent) && !included.Contains(d.Id)))
                {
                    added = included.Add(department.Id) || added;
                }
            }

            employees = employees.Where(employee => employee.DepartmentId != null && included.Contains(employee.DepartmentId.Value));
        }

        return await BuildAsync(employees, year, month, cancellationToken);
    }

    /// <summary>The given people, for example a manager and the people who report to them.</summary>
    public async Task<Result<LeaveCalendarResponse>> GetForEmployeesAsync(
        IReadOnlyCollection<Guid> employeeIds, int year, int month, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(employeeIds);

        return Check(year, month) is { } error
            ? error
            : await BuildAsync(db.Employees.AsNoTracking().Where(employee => employeeIds.Contains(employee.Id)), year, month, cancellationToken);
    }

    private async Task<LeaveCalendarResponse> BuildAsync(IQueryable<Employee> employees, int year, int month, CancellationToken cancellationToken)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        // Only people employed at some point of the month.
        var people = await employees
            .Where(employee => employee.HireDate <= last && (employee.TerminationDate == null || employee.TerminationDate >= first))
            .OrderBy(employee => employee.LastName).ThenBy(employee => employee.FirstName)
            .Select(employee => new
            {
                employee.Id,
                employee.FirstName,
                employee.LastName,
                Department = db.Departments.Where(d => d.Id == employee.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                Position = db.Positions.Where(p => p.Id == employee.PositionId).Select(p => p.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var ids = people.Select(person => person.Id).ToList();
        var leave = await db.LeaveRequests.AsNoTracking()
            .Where(request => ids.Contains(request.EmployeeId) && request.StartDate <= last && request.EndDate >= first
                && (request.Status == LeaveStatus.Approved || request.Status == LeaveStatus.Pending))
            .OrderBy(request => request.StartDate)
            .ToListAsync(cancellationToken);

        var byPerson = leave.ToLookup(request => request.EmployeeId);
        var holidays = PolishCalendar.Holidays(year)
            .Where(holiday => holiday.Date.Month == month)
            .Select(holiday => new HolidayResponse(holiday.Date, holiday.Name))
            .ToList();

        return new LeaveCalendarResponse(
            year,
            month,
            holidays,
            people.Select(person => new LeaveCalendarPerson(
                person.Id,
                person.FirstName,
                person.LastName,
                person.Department,
                person.Position,
                byPerson[person.Id]
                    .Select(request => new LeaveCalendarEntry(
                        request.Id, (LeaveKind)request.Type, request.StartDate, request.EndDate, (LeaveState)request.Status, request.IsHourly, request.Hours))
                    .ToList()))
                .ToList());
    }

    private static AppError? Check(int year, int month) =>
        year is < 2000 or > 2100 ? LeaveErrors.YearOutOfRange
        : month is < 1 or > 12 ? LeaveErrors.MonthOutOfRange
        : null;
}
