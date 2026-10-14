using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Application.Company;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.WorkTime;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;
using PersoNet.Domain.WorkTime;

namespace PersoNet.Application.WorkTime;

/// <summary>
/// Employees' timesheets ("ewidencja czasu pracy", art. 149 KP): days typed by HR, days worked out from clock events of a
/// time clock system, absences, and approved leave from the leave module, with the month's norm, overtime and night work.
/// </summary>
public sealed class WorkTimeService(IAppDbContext db, ITenantContext tenant, CompanyService company, TimeProvider time)
{
    public const int MaxImportEvents = 50_000;

    /// <summary>Where a day filled from the plan starts.</summary>
    public static readonly TimeOnly PlannedStart = new(8, 0);

    public async Task<Result<TimesheetResponse>> GetTimesheetAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken)
    {
        if (CheckMonth(year, month) is { } error)
        {
            return error;
        }

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
        return employee is null ? WorkTimeErrors.EmployeeNotFound : await BuildAsync(employee, year, month, cancellationToken);
    }

    /// <summary>HR types a day: the hours worked or an absence. The day is manual from then on, whatever the clock says.</summary>
    public async Task<Result<TimesheetResponse>> SaveDayAsync(Guid employeeId, DateOnly date, SaveWorkDayRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return WorkTimeErrors.NoTenant;
        }

        if ((CheckMonth(date.Year, date.Month) ?? Check(request)) is { } formError)
        {
            return formError;
        }

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            return WorkTimeErrors.EmployeeNotFound;
        }

        if (!employee.IsEmployedOn(date))
        {
            return WorkTimeErrors.NotEmployed;
        }

        if (await HasFullDayLeaveAsync(employeeId, date, cancellationToken))
        {
            return WorkTimeErrors.LeaveOnDay;
        }

        var day = await db.WorkDays.FirstOrDefaultAsync(day => day.EmployeeId == employeeId && day.Date == date, cancellationToken);

        try
        {
            if (request.Absence is { } absence)
            {
                if (day is null)
                {
                    db.WorkDays.Add(WorkDay.Absent(tenantId, employeeId, date, (AbsenceType)absence, request.Note));
                }
                else
                {
                    day.ChangeToAbsence((AbsenceType)absence, request.Note);
                }
            }
            else if (day is null)
            {
                db.WorkDays.Add(WorkDay.Worked(tenantId, employeeId, date, request.Start!.Value, request.End!.Value, request.BreakMinutes, request.Note));
            }
            else
            {
                day.ChangeToWork(request.Start!.Value, request.End!.Value, request.BreakMinutes, request.Note);
            }
        }
        catch (DomainException exception)
        {
            return WorkTimeErrors.Invalid(exception);
        }

        await db.SaveChangesAsync(cancellationToken);
        return await BuildAsync(employee, date.Year, date.Month, cancellationToken);
    }

    /// <summary>Removes what HR typed for a day. When the time clock recorded the day, its hours come back from the events.</summary>
    public async Task<Result<TimesheetResponse>> ClearDayAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return WorkTimeErrors.NoTenant;
        }

        if (CheckMonth(date.Year, date.Month) is { } error)
        {
            return error;
        }

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            return WorkTimeErrors.EmployeeNotFound;
        }

        if (await db.WorkDays.FirstOrDefaultAsync(day => day.EmployeeId == employeeId && day.Date == date, cancellationToken) is { } day)
        {
            db.WorkDays.Remove(day);
            await db.SaveChangesAsync(cancellationToken);
        }

        await RecalculateAsync(tenantId, employeeId, date, date, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return await BuildAsync(employee, date.Year, date.Month, cancellationToken);
    }

    /// <summary>
    /// Fills the empty working days of the month with the planned hours from <see cref="PlannedStart"/>, so HR only corrects
    /// the exceptions. Days with leave, with anything recorded, or without an employment contract stay as they are.
    /// </summary>
    public async Task<Result<TimesheetResponse>> FillFromPlanAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken)
    {
        if (tenant.TenantId is not { } tenantId)
        {
            return WorkTimeErrors.NoTenant;
        }

        if (CheckMonth(year, month) is { } error)
        {
            return error;
        }

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            return WorkTimeErrors.EmployeeNotFound;
        }

        var month1 = await LoadMonthAsync(employee, year, month, cancellationToken);
        foreach (var date in month1.Dates)
        {
            var planned = WorkTimeRules.PlannedMinutes(date, month1.WorkingTime(date));
            var leave = month1.LeaveOn(date, includePending: true);
            var minutes = planned - (leave is { IsHourly: true } ? (int)(leave.Hours * 60) : leave is null ? 0 : planned);

            if (minutes <= 0 || month1.Days.ContainsKey(date))
            {
                continue;
            }

            db.WorkDays.Add(WorkDay.Worked(tenantId, employeeId, date, PlannedStart, PlannedStart.AddMinutes(minutes), 0, null));
        }

        await db.SaveChangesAsync(cancellationToken);
        return await BuildAsync(employee, year, month, cancellationToken);
    }

    /// <summary>Sets the employee's identifier in the time clock system, which must not belong to anyone else.</summary>
    public async Task<Result> SetClockIdAsync(Guid employeeId, SaveClockIdRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var employee = await db.Employees.FirstOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            return WorkTimeErrors.EmployeeNotFound;
        }

        var clockId = string.IsNullOrWhiteSpace(request.ClockId) ? null : request.ClockId.Trim();
        if (clockId is not null && (clockId.Length > Employee.ClockIdMaxLength || clockId.Any(char.IsWhiteSpace)))
        {
            return WorkTimeErrors.ClockIdInvalid;
        }

        if (clockId is not null && await db.Employees.AnyAsync(other => other.Id != employeeId && other.ClockId == clockId, cancellationToken))
        {
            return WorkTimeErrors.ClockIdTaken;
        }

        employee.ChangeClockId(clockId);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Stores clock events from a file or a connected system and works out the days they touch again. Events already
    /// stored are skipped, so importing the same file twice changes nothing; events of unknown people are left out and
    /// their identifiers reported, so HR can add them to the right employees.
    /// </summary>
    public async Task<Result<ImportClockEventsResponse>> ImportEventsAsync(ImportClockEventsRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return WorkTimeErrors.NoTenant;
        }

        if (request.Origin is not (ClockEventOrigin.File or ClockEventOrigin.Integration))
        {
            return WorkTimeErrors.ImportOrigin;
        }

        if (request.Events is null || request.Events.Count == 0)
        {
            return WorkTimeErrors.ImportEmpty;
        }

        if (request.Events.Count > MaxImportEvents)
        {
            return WorkTimeErrors.ImportTooLarge;
        }

        if (request.Events.Any(e => (e.EmployeeId is null && string.IsNullOrWhiteSpace(e.ClockId))
                || e.OccurredAt.Year is < 2000 or > 2100 || !Enum.IsDefined((ClockDirection)e.Direction)))
        {
            return WorkTimeErrors.EventInvalid;
        }

        // Who the events are for: by id, or by the identifier in the time clock system.
        var ids = request.Events.Where(e => e.EmployeeId is not null).Select(e => e.EmployeeId!.Value).Distinct().ToList();
        var clockIds = request.Events.Where(e => e.EmployeeId is null).Select(e => e.ClockId!.Trim()).Distinct(StringComparer.Ordinal).ToList();
        var people = await db.Employees.AsNoTracking()
            .Where(employee => ids.Contains(employee.Id) || (employee.ClockId != null && clockIds.Contains(employee.ClockId)))
            .Select(employee => new { employee.Id, employee.ClockId })
            .ToListAsync(cancellationToken);
        var known = people.Select(person => person.Id).ToHashSet();
        var byClockId = people.Where(person => person.ClockId is not null).ToDictionary(person => person.ClockId!, person => person.Id, StringComparer.Ordinal);

        var unknown = new SortedSet<string>(StringComparer.Ordinal);
        var incoming = new List<(Guid EmployeeId, DateTime At, ClockDirection Direction, string? Device)>();
        foreach (var input in request.Events)
        {
            Guid? employeeId = input.EmployeeId is { } id ? (known.Contains(id) ? id : null)
                : byClockId.TryGetValue(input.ClockId!.Trim(), out var found) ? found : null;

            if (employeeId is null)
            {
                unknown.Add(input.EmployeeId?.ToString() ?? input.ClockId!.Trim());
                continue;
            }

            incoming.Add((employeeId.Value, ClockEvent.WholeSeconds(input.OccurredAt), (ClockDirection)input.Direction, input.Device));
        }

        var added = 0;
        var duplicates = 0;
        var daysUpdated = 0;
        var now = time.GetUtcNow();

        foreach (var employee in incoming.GroupBy(e => e.EmployeeId))
        {
            var from = employee.Min(e => e.At);
            var to = employee.Max(e => e.At);
            var stored = (await db.ClockEvents.AsNoTracking()
                    .Where(e => e.EmployeeId == employee.Key && e.OccurredAt >= from && e.OccurredAt <= to)
                    .Select(e => new { e.OccurredAt, e.Direction })
                    .ToListAsync(cancellationToken))
                .Select(e => (e.OccurredAt, e.Direction))
                .ToHashSet();

            foreach (var e in employee)
            {
                if (!stored.Add((e.At, e.Direction)))
                {
                    duplicates++;
                    continue;
                }

                try
                {
                    db.ClockEvents.Add(ClockEvent.Record(tenantId, employee.Key, e.At, e.Direction, (ClockEventSource)request.Origin, e.Device, now));
                }
                catch (DomainException exception)
                {
                    return WorkTimeErrors.Invalid(exception);
                }

                added++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        foreach (var employee in incoming.GroupBy(e => e.EmployeeId))
        {
            daysUpdated += await RecalculateAsync(
                tenantId, employee.Key, DateOnly.FromDateTime(employee.Min(e => e.At)), DateOnly.FromDateTime(employee.Max(e => e.At)), cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportClockEventsResponse(request.Events.Count, added, duplicates, unknown.Take(100).ToList(), daysUpdated);
    }

    /// <summary>
    /// Works out the days from <paramref name="from"/> to <paramref name="to"/> again from the clock events. Days HR typed
    /// keep their hours. Returns how many days changed. The caller saves.
    /// </summary>
    private async Task<int> RecalculateAsync(Guid tenantId, Guid employeeId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        // A shift may start the evening before the first day or end the morning after the last one.
        var start = from.AddDays(-1).ToDateTime(TimeOnly.MinValue);
        var end = to.AddDays(2).ToDateTime(TimeOnly.MinValue);
        var events = await db.ClockEvents.AsNoTracking()
            .Where(e => e.EmployeeId == employeeId && e.OccurredAt >= start && e.OccurredAt < end)
            .Select(e => new { e.OccurredAt, e.Direction })
            .ToListAsync(cancellationToken);

        var clockDays = ClockPairing.Pair(events.Select(e => (e.OccurredAt, e.Direction)))
            .Where(day => day.Date >= from && day.Date <= to)
            .ToDictionary(day => day.Date);
        var saved = await db.WorkDays.Where(day => day.EmployeeId == employeeId && day.Date >= from && day.Date <= to).ToListAsync(cancellationToken);
        var changed = 0;

        foreach (var day in saved.Where(day => day.Origin == WorkDayOrigin.Clock))
        {
            if (clockDays.TryGetValue(day.Date, out var clock))
            {
                day.ApplyClock(clock);
            }
            else
            {
                db.WorkDays.Remove(day);
            }

            changed++;
        }

        foreach (var clock in clockDays.Values.Where(clock => saved.All(day => day.Date != clock.Date)))
        {
            db.WorkDays.Add(WorkDay.FromClock(tenantId, employeeId, clock));
            changed++;
        }

        return changed;
    }

    private async Task<bool> HasFullDayLeaveAsync(Guid employeeId, DateOnly date, CancellationToken cancellationToken) =>
        PolishCalendar.IsWorkingDay(date) && await db.LeaveRequests.AsNoTracking().AnyAsync(
            leave => leave.EmployeeId == employeeId && leave.Status == LeaveStatus.Approved && !leave.IsHourly && leave.StartDate <= date && leave.EndDate >= date,
            cancellationToken);

    private async Task<TimesheetResponse> BuildAsync(Employee employee, int year, int month, CancellationToken cancellationToken)
    {
        var data = await LoadMonthAsync(employee, year, month, cancellationToken);
        var source = await company.GetWorkTimeSourceAsync(cancellationToken);

        var days = new List<TimesheetDay>();
        int norm = 0, planned = 0, worked = 0, overtime = 0, overPlan = 0, dayOff = 0, sundayHoliday = 0, night = 0, leaveTotal = 0, absenceTotal = 0;
        int daysWorked = 0, daysWithIssues = 0;

        foreach (var date in data.Dates)
        {
            var employed = employee.IsEmployedOn(date);
            var workingTime = employed ? data.WorkingTime(date) : null;
            var record = data.Days.GetValueOrDefault(date);
            var minutes = record?.WorkedMinutes ?? 0;
            var nightMinutes = record is { StartTime: { } start, EndTime: { } end } && minutes > 0 ? WorkTimeRules.NightMinutes(start, end, minutes) : 0;
            var figures = WorkTimeRules.Figures(date, workingTime, minutes, nightMinutes);

            var leave = figures.Kind == DayKind.Working ? data.LeaveOn(date, includePending: false) : null;
            var leaveMinutes = leave is null ? 0 : leave.IsHourly ? (int)(leave.Hours * 60) : figures.Planned;
            var absenceMinutes = record?.Absence is not null ? figures.Planned : 0;

            norm += employed ? WorkTimeRules.NormMinutes(date, workingTime) : 0;
            planned += figures.Planned;
            worked += figures.Worked;
            overtime += figures.Overtime;
            overPlan += figures.OverPlan;
            dayOff += figures.OnDayOff;
            sundayHoliday += figures.OnSundayOrHoliday;
            night += figures.Night;
            leaveTotal += leaveMinutes;
            absenceTotal += absenceMinutes;
            daysWorked += figures.Worked > 0 ? 1 : 0;
            daysWithIssues += record is { Problem: not ClockProblem.None } ? 1 : 0;

            days.Add(new TimesheetDay(
                date,
                (TimesheetDayKind)figures.Kind,
                PolishCalendar.HolidayName(date),
                employed,
                Hours(figures.Planned),
                record?.StartTime,
                record?.EndTime,
                record?.BreakMinutes ?? 0,
                Hours(figures.Worked),
                Hours(figures.Overtime),
                Hours(figures.OverPlan),
                Hours(figures.OnDayOff),
                Hours(figures.OnSundayOrHoliday),
                Hours(figures.Night),
                (AbsenceKind?)record?.Absence,
                (LeaveKind?)leave?.Type,
                Hours(leaveMinutes + absenceMinutes),
                record?.Note,
                record is not null,
                record?.Origin == WorkDayOrigin.Manual,
                (ClockIssue)(record?.Problem ?? ClockProblem.None),
                data.Events.Where(e => e.Date == date)
                    .Select(e => new ClockEventResponse(e.Id, e.OccurredAt, (ClockEventDirection)e.Direction, (ClockEventOrigin)e.Source, e.Device))
                    .ToList()));
        }

        return new TimesheetResponse(
            employee.Id,
            employee.Number,
            employee.FirstName,
            employee.LastName,
            employee.ClockId,
            year,
            month,
            source,
            days,
            new TimesheetTotals(
                Hours(norm), Hours(planned), Hours(worked), Hours(overtime), Hours(overPlan), Hours(dayOff), Hours(sundayHoliday), Hours(night),
                Hours(leaveTotal), Hours(absenceTotal), daysWorked, daysWithIssues));
    }

    private async Task<MonthData> LoadMonthAsync(Employee employee, int year, int month, CancellationToken cancellationToken)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var from = first.ToDateTime(TimeOnly.MinValue);
        var to = last.AddDays(1).ToDateTime(TimeOnly.MinValue);

        var contracts = await db.Contracts.AsNoTracking()
            .Where(contract => contract.EmployeeId == employee.Id && contract.StartDate <= last && (contract.EndDate == null || contract.EndDate >= first))
            .ToListAsync(cancellationToken);
        var days = await db.WorkDays.AsNoTracking()
            .Where(day => day.EmployeeId == employee.Id && day.Date >= first && day.Date <= last)
            .ToDictionaryAsync(day => day.Date, cancellationToken);
        var events = await db.ClockEvents.AsNoTracking()
            .Where(e => e.EmployeeId == employee.Id && e.OccurredAt >= from && e.OccurredAt < to)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(cancellationToken);
        var leave = await db.LeaveRequests.AsNoTracking()
            .Where(request => request.EmployeeId == employee.Id
                && (request.Status == LeaveStatus.Approved || request.Status == LeaveStatus.Pending)
                && request.StartDate <= last && request.EndDate >= first)
            .ToListAsync(cancellationToken);

        return new MonthData(first, last, contracts.Where(contract => contract.IsEmployment).ToList(), days, events, leave);
    }

    private static AppError? CheckMonth(int year, int month) =>
        year is < 2000 or > 2100 ? WorkTimeErrors.YearOutOfRange
            : month is < 1 or > 12 ? WorkTimeErrors.MonthOutOfRange
            : null;

    // The specific rule is reported with its own code, so the screen can show it next to the right field.
    private static AppError? Check(SaveWorkDayRequest request)
    {
        if (request.Note?.Trim().Length > WorkDay.NoteMaxLength)
        {
            return WorkTimeErrors.NoteTooLong;
        }

        if (request.Absence is { } absence)
        {
            return request.Start is not null || request.End is not null ? WorkTimeErrors.WorkOrAbsence
                : !Enum.IsDefined((AbsenceType)absence) ? WorkTimeErrors.UnknownAbsence
                : null;
        }

        if (request.Start is not { } start || request.End is not { } end)
        {
            return WorkTimeErrors.TimesRequired;
        }

        start = WorkTimeRules.ToMinute(start);
        end = WorkTimeRules.ToMinute(end);
        if (start == end)
        {
            return WorkTimeErrors.SameTimes;
        }

        return request.BreakMinutes < 0 || request.BreakMinutes > WorkDay.MaxBreakMinutes || request.BreakMinutes >= WorkTimeRules.Span(start, end)
            ? WorkTimeErrors.BreakInvalid
            : null;
    }

    private static decimal Hours(int minutes) => Math.Round(minutes / 60m, 2);

    /// <summary>What a month of one employee consists of: contracts, recorded days, clock events and leave.</summary>
    private sealed record MonthData(
        DateOnly First,
        DateOnly Last,
        List<EmploymentContract> Contracts,
        Dictionary<DateOnly, WorkDay> Days,
        List<ClockEvent> Events,
        List<LeaveRequest> Leave)
    {
        public IEnumerable<DateOnly> Dates
        {
            get
            {
                for (var date = First; date <= Last; date = date.AddDays(1))
                {
                    yield return date;
                }
            }
        }

        /// <summary>The working time of the employment contract on the day; null without one.</summary>
        public decimal? WorkingTime(DateOnly date) =>
            Contracts.Where(contract => contract.StartDate <= date && (contract.LastDay is null || contract.LastDay >= date))
                .OrderByDescending(contract => contract.StartDate)
                .Select(contract => contract.WorkingTime)
                .FirstOrDefault();

        public LeaveRequest? LeaveOn(DateOnly date, bool includePending) =>
            Leave.FirstOrDefault(leave => leave.StartDate <= date && leave.EndDate >= date
                && (leave.Status == LeaveStatus.Approved || (includePending && leave.Status == LeaveStatus.Pending)));
    }
}
