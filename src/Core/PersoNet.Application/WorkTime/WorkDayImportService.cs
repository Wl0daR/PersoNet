using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Absences;
using PersoNet.Contracts.WorkTime;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Leave;
using PersoNet.Domain.WorkTime;

namespace PersoNet.Application.WorkTime;

/// <summary>
/// Moves the days worked in an earlier system into the timesheets of employees already imported, matched by personnel
/// number. A day already in the timesheet is left as it is, so the same files can be imported again; a day of approved
/// full-day leave is not turned into work, as HR could not type it either.
/// </summary>
public sealed class WorkDayImportService(IAppDbContext db, ITenantContext tenant)
{
    public const int MaxRows = 5000;

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public async Task<Result<ImportWorkDaysResponse>> ImportAsync(ImportWorkDaysRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return WorkTimeErrors.NoTenant;
        }

        if (request.Days is null || request.Days.Count == 0)
        {
            return WorkTimeErrors.DaysImportEmpty;
        }

        if (request.Days.Count > MaxRows)
        {
            return WorkTimeErrors.DaysImportTooLarge;
        }

        var numbers = request.Days.Select(row => Number(row.Number)).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(employee => numbers.Contains(employee.Number))
            .ToDictionaryAsync(employee => employee.Number, employee => employee.Id, StringComparer.Ordinal, cancellationToken);
        var ids = employees.Values.ToList();
        var first = request.Days.Min(row => row.Date);
        var last = request.Days.Max(row => row.Date);

        var recorded = (await db.WorkDays.AsNoTracking()
                .Where(day => ids.Contains(day.EmployeeId) && day.Date >= first && day.Date <= last)
                .Select(day => new { day.EmployeeId, day.Date })
                .ToListAsync(cancellationToken))
            .Select(day => (day.EmployeeId, day.Date))
            .ToHashSet();
        var leave = (await db.LeaveRequests.AsNoTracking()
                .Where(leave => ids.Contains(leave.EmployeeId) && leave.Status == LeaveStatus.Approved && !leave.IsHourly
                    && leave.StartDate <= last && leave.EndDate >= first)
                .Select(leave => new { leave.EmployeeId, leave.StartDate, leave.EndDate })
                .ToListAsync(cancellationToken))
            .ToLookup(leave => leave.EmployeeId, leave => (leave.StartDate, leave.EndDate));

        var problems = new List<ImportLeaveProblem>();
        int added = 0, existing = 0;
        foreach (var row in request.Days)
        {
            var number = Number(row.Number);
            if (!employees.TryGetValue(number, out var employeeId))
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, $"Nie ma pracownika o numerze {number}; najpierw zaimportuj pracowników."));
                continue;
            }

            var date = row.Date.ToString("d", Polish);
            if (recorded.Contains((employeeId, row.Date)))
            {
                existing++;
                continue;
            }

            if (leave[employeeId].Any(period => period.StartDate <= row.Date && row.Date <= period.EndDate))
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, $"{date}: w tym dniu pracownik ma zatwierdzony urlop."));
                continue;
            }

            try
            {
                db.WorkDays.Add(WorkDay.Worked(tenantId, employeeId, row.Date, row.Start, row.End, 0, null));
                recorded.Add((employeeId, row.Date));
                added++;
            }
            catch (DomainException)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, $"{date}: praca musi kończyć się o innej godzinie, niż się zaczyna."));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportWorkDaysResponse(added, existing, problems);
    }

    private static string Number(string? number) => number?.Trim().ToUpperInvariant() ?? string.Empty;
}
