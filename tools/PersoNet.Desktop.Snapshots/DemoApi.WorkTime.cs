using PersoNet.Contracts.Leave;
using PersoNet.Contracts.WorkTime;

namespace PersoNet.Desktop.Snapshots;

/// <summary>A month of clock events from the reader at the main entrance, with the usual exceptions HR sees.</summary>
internal sealed partial class DemoApi
{
    private const string Entrance = "Wejście główne";

    public Task<TimesheetResponse> GetTimesheetAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken)
    {
        var employee = _employees.First(candidate => candidate.Id == employeeId);
        var holidays = GetHolidaysAsync(year, cancellationToken).Result.ToDictionary(holiday => holiday.Date, holiday => holiday.Name);
        var today = DateOnly.FromDateTime(DateTime.Today);
        var days = new List<TimesheetDay>();
        var workingDay = 0;

        for (var date = new DateOnly(year, month, 1); date.Month == month; date = date.AddDays(1))
        {
            var kind = date.DayOfWeek == DayOfWeek.Sunday ? TimesheetDayKind.Sunday
                : holidays.ContainsKey(date) ? TimesheetDayKind.Holiday
                : date.DayOfWeek == DayOfWeek.Saturday ? TimesheetDayKind.DayOff
                : TimesheetDayKind.Working;
            var planned = kind == TimesheetDayKind.Working ? 8m : 0m;
            var empty = Day(date, kind, holidays.GetValueOrDefault(date), planned);

            if (kind != TimesheetDayKind.Working || date >= today)
            {
                days.Add(empty);
                continue;
            }

            workingDay++;
            days.Add(workingDay switch
            {
                4 or 5 => empty with { Leave = LeaveKind.Annual, AbsenceHours = 8m },
                8 => empty with { Absence = AbsenceKind.Sickness, AbsenceHours = 8m, IsRecorded = true, IsManual = true, Note = "zwolnienie lekarskie" },
                11 => empty with
                {
                    Start = new TimeOnly(7, 52),
                    IsRecorded = true,
                    Issue = ClockIssue.MissingOut,
                    Events = [Event(date, new TimeOnly(7, 52), ClockEventDirection.In)],
                },
                13 => Worked(empty, new TimeOnly(7, 30), new TimeOnly(18, 15), 0, null) with { Note = "inwentaryzacja magazynu" },
                16 => Worked(empty, new TimeOnly(14, 0), new TimeOnly(22, 30), 30, null) with { IsManual = true, Note = "druga zmiana" },
                _ => Worked(empty, new TimeOnly(7, 50 + (date.Day % 9)), new TimeOnly(16, date.Day % 7), 0, null),
            });
        }

        var totals = new TimesheetTotals(
            days.Sum(day => day.PlannedHours),
            days.Sum(day => day.PlannedHours),
            days.Sum(day => day.WorkedHours),
            days.Sum(day => day.OvertimeHours),
            0m,
            0m,
            0m,
            days.Sum(day => day.NightHours),
            days.Where(day => day.Leave is not null).Sum(day => day.AbsenceHours),
            days.Where(day => day.Absence is not null).Sum(day => day.AbsenceHours),
            days.Count(day => day.WorkedHours > 0),
            days.Count(day => day.Issue != ClockIssue.None));

        return Task.FromResult(new TimesheetResponse(
            employee.Id, employee.Number, employee.FirstName, employee.LastName, "0042", year, month, _company.WorkTimeSource, days, totals));
    }

    public Task<TimesheetResponse> FillFromPlanAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<TimesheetResponse> SaveDayAsync(Guid employeeId, DateOnly day, SaveWorkDayRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<TimesheetResponse> ClearDayAsync(Guid employeeId, DateOnly day, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SetClockIdAsync(Guid employeeId, SaveClockIdRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ImportClockEventsResponse> ImportEventsAsync(ImportClockEventsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ImportWorkDaysResponse> ImportDaysAsync(ImportWorkDaysRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    private static TimesheetDay Day(DateOnly date, TimesheetDayKind kind, string? holiday, decimal planned) =>
        new(date, kind, holiday, true, planned, null, null, 0, 0m, 0m, 0m, 0m, 0m, 0m, null, null, 0m, null, false, false, ClockIssue.None, []);

    private static TimesheetDay Worked(TimesheetDay day, TimeOnly start, TimeOnly end, int breakMinutes, string? note)
    {
        var worked = (decimal)Math.Round(((end - start).TotalMinutes - breakMinutes) / 60, 2);
        var night = end > new TimeOnly(22, 0) ? (decimal)Math.Round((end - new TimeOnly(22, 0)).TotalMinutes / 60, 2) : 0m;
        return day with
        {
            Start = start,
            End = end,
            BreakMinutes = breakMinutes,
            WorkedHours = worked,
            OvertimeHours = Math.Max(0m, worked - 8m),
            NightHours = night,
            Note = note,
            IsRecorded = true,
            Events = [Event(day.Date, start, ClockEventDirection.In), Event(day.Date, end, ClockEventDirection.Out)],
        };
    }

    private static ClockEventResponse Event(DateOnly date, TimeOnly time, ClockEventDirection direction) =>
        new(Guid.NewGuid(), date.ToDateTime(time), direction, ClockEventOrigin.Integration, Entrance);
}
