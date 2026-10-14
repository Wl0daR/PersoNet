using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Reports;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;

namespace PersoNet.Application.Reports;

/// <summary>
/// Company-wide HR reports: headcount on a day, the use of annual leave in a year and contracts that are about to end.
/// They read the whole company, so they need their own permission (<see cref="Permissions.ReportsRead"/>).
/// </summary>
public sealed class ReportService(IAppDbContext db, TimeProvider time)
{
    public const int DefaultDaysAhead = 60;

    public const int MaxDaysAhead = 366;

    private static readonly DateOnly MinDate = new(2000, 1, 1);
    private static readonly DateOnly MaxDate = new(2100, 12, 31);

    public async Task<Result<HeadcountReport>> GetHeadcountAsync(DateOnly? date, CancellationToken cancellationToken)
    {
        var day = date ?? Today;
        if (day < MinDate || day > MaxDate)
        {
            return ReportErrors.DateOutOfRange;
        }

        var people = await EmployedBetween(day, day)
            .Select(employee => new
            {
                employee.Id,
                employee.Number,
                employee.FirstName,
                employee.LastName,
                Department = db.Departments.Where(d => d.Id == employee.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                Position = db.Positions.Where(p => p.Id == employee.PositionId).Select(p => p.Name).FirstOrDefault(),
                employee.HireDate,
            })
            .ToListAsync(cancellationToken);

        var ids = people.Select(person => person.Id).ToList();
        var contracts = (await db.Contracts.AsNoTracking()
                .Where(contract => ids.Contains(contract.EmployeeId) && contract.StartDate <= day
                    && (contract.EndDate == null || contract.EndDate >= day)
                    && (contract.TerminatedOn == null || contract.TerminatedOn >= day))
                .ToListAsync(cancellationToken))
            .ToLookup(contract => contract.EmployeeId);

        var rows = people
            .Select(person =>
            {
                // An employment contract counts first; a civil one only when the person has nothing else.
                var contract = contracts[person.Id].OrderByDescending(c => c.IsEmployment).ThenByDescending(c => c.StartDate).FirstOrDefault();
                return new HeadcountPerson(
                    person.Id,
                    person.Number,
                    person.FirstName,
                    person.LastName,
                    person.Department,
                    person.Position,
                    person.HireDate,
                    (ContractKind?)contract?.Type,
                    contract?.LastDay,
                    contract?.WorkingTime);
            })
            .ToList();

        var departments = rows
            .GroupBy(row => row.DepartmentName)
            .OrderBy(group => group.Key is null).ThenBy(group => group.Key, StringComparer.CurrentCulture)
            .Select(group => new HeadcountDepartment(group.Key, group.Count(), group.Sum(FullTime)))
            .ToList();

        return new HeadcountReport(day, rows.Count, rows.Sum(FullTime), departments, rows);
    }

    public async Task<Result<LeaveUsageReport>> GetLeaveUsageAsync(int? year, CancellationToken cancellationToken)
    {
        var today = Today;
        var reportYear = year ?? today.Year;
        if (reportYear is < 2000 or > 2100)
        {
            return ReportErrors.YearOutOfRange;
        }

        var first = new DateOnly(reportYear, 1, 1);
        var last = new DateOnly(reportYear, 12, 31);
        var people = await EmployedBetween(first, last)
            .Select(employee => new
            {
                employee.Id,
                employee.Number,
                employee.FirstName,
                employee.LastName,
                Department = db.Departments.Where(d => d.Id == employee.DepartmentId).Select(d => d.Name).FirstOrDefault(),
            })
            .ToListAsync(cancellationToken);

        var ids = people.Select(person => person.Id).ToList();
        var entitlements = await db.LeaveEntitlements.AsNoTracking()
            .Where(entitlement => ids.Contains(entitlement.EmployeeId) && entitlement.Year == reportYear)
            .ToDictionaryAsync(entitlement => entitlement.EmployeeId, cancellationToken);

        // Annual leave and leave on demand, the same requests the balance counts.
        var annual = (await db.LeaveRequests.AsNoTracking()
                .Where(request => ids.Contains(request.EmployeeId) && request.StartDate.Year == reportYear
                    && (request.Type == LeaveType.Annual || request.Type == LeaveType.OnDemand)
                    && (request.Status == LeaveStatus.Pending || request.Status == LeaveStatus.Approved))
                .Select(request => new { request.EmployeeId, request.Type, request.Status, request.Days, request.Hours })
                .ToListAsync(cancellationToken))
            .ToLookup(request => request.EmployeeId);

        // The working day today, or on the nearest day of that year, as in the balance.
        var day = today.Year == reportYear ? today : today.Year < reportYear ? first : last;
        var workingTimes = (await db.Contracts.AsNoTracking()
                .Where(contract => ids.Contains(contract.EmployeeId) && contract.StartDate <= day && (contract.EndDate == null || contract.EndDate >= day))
                .Select(contract => new { contract.EmployeeId, contract.StartDate, contract.WorkingTime })
                .ToListAsync(cancellationToken))
            .ToLookup(contract => contract.EmployeeId);

        var rows = people
            .Select(person =>
            {
                entitlements.TryGetValue(person.Id, out var entitlement);
                var requests = annual[person.Id].ToList();
                var used = requests.Where(request => request.Status == LeaveStatus.Approved).Sum(request => request.Hours);
                var pending = requests.Where(request => request.Status == LeaveStatus.Pending).Sum(request => request.Hours);
                var total = entitlement?.TotalHours ?? 0;
                var workingTime = workingTimes[person.Id].OrderByDescending(contract => contract.StartDate).Select(contract => contract.WorkingTime).FirstOrDefault();

                return new LeaveUsageRow(
                    person.Id,
                    person.Number,
                    person.FirstName,
                    person.LastName,
                    person.Department,
                    entitlement is not null,
                    entitlement?.Days ?? 0,
                    entitlement?.CarriedOverDays ?? 0,
                    total,
                    used,
                    pending,
                    total - used - pending,
                    requests.Where(request => request.Type == LeaveType.OnDemand).Sum(request => request.Days),
                    Math.Round(LeaveEntitlement.HoursPerDay * (workingTime ?? 1m), 2));
            })
            .ToList();

        return new LeaveUsageReport(reportYear, rows);
    }

    public async Task<Result<ContractsEndingReport>> GetContractsEndingAsync(DateOnly? from, int? days, CancellationToken cancellationToken)
    {
        var start = from ?? Today;
        var length = days ?? DefaultDaysAhead;
        if (start < MinDate || start > MaxDate)
        {
            return ReportErrors.DateOutOfRange;
        }

        if (length is < 1 or > MaxDaysAhead)
        {
            return ReportErrors.DaysOutOfRange;
        }

        var end = start.AddDays(length - 1);

        // Contracts ended early (with the employment) are left out: there is nothing left to decide about them.
        var ending = await db.Contracts.AsNoTracking()
            .Where(contract => contract.EndDate >= start && contract.EndDate <= end && contract.TerminatedOn == null)
            .Join(
                db.Employees.AsNoTracking(),
                contract => contract.EmployeeId,
                employee => employee.Id,
                (contract, employee) => new
                {
                    Contract = contract,
                    employee.Number,
                    employee.FirstName,
                    employee.LastName,
                    Department = db.Departments.Where(d => d.Id == employee.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                    Position = db.Positions.Where(p => p.Id == employee.PositionId).Select(p => p.Name).FirstOrDefault(),
                    HasNext = db.Contracts.Any(next => next.EmployeeId == contract.EmployeeId && next.Id != contract.Id && next.StartDate > contract.StartDate
                        && (next.EndDate == null || next.EndDate > contract.EndDate)),
                })
            .OrderBy(row => row.Contract.EndDate).ThenBy(row => row.LastName).ThenBy(row => row.FirstName)
            .ToListAsync(cancellationToken);

        var rows = ending
            .Select(row => new ContractEndingRow(
                row.Contract.Id,
                row.Contract.EmployeeId,
                row.Number,
                row.FirstName,
                row.LastName,
                row.Department,
                row.Position,
                (ContractKind)row.Contract.Type,
                row.Contract.StartDate,
                row.Contract.EndDate!.Value,
                row.Contract.WorkingTime,
                row.HasNext))
            .ToList();

        return new ContractsEndingReport(start, end, rows);
    }

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    private static decimal FullTime(HeadcountPerson person) =>
        person.ContractKind is { } kind && EmploymentContract.IsEmploymentType((ContractType)kind) ? person.WorkingTime ?? 0m : 0m;

    private IQueryable<Employee> EmployedBetween(DateOnly first, DateOnly last) =>
        db.Employees.AsNoTracking()
            .Where(employee => employee.HireDate <= last && (employee.TerminationDate == null || employee.TerminationDate >= first))
            .OrderBy(employee => employee.LastName).ThenBy(employee => employee.FirstName);
}
