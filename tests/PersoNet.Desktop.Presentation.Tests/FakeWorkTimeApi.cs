using System.Net;
using PersoNet.ApiClient;
using PersoNet.ApiClient.WorkTime;
using PersoNet.Contracts.WorkTime;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>
/// In-memory stand-in for the working time API: 8 planned hours Monday to Friday, the days typed so far, and the
/// server's answers to a few mistakes (the same start and end, a time clock id that is already taken).
/// </summary>
internal sealed class FakeWorkTimeApi : IWorkTimeApi
{
    public const string TakenClockId = "0042";

    public Dictionary<DateOnly, SaveWorkDayRequest> Days { get; } = [];

    public string? ClockId { get; private set; }

    public List<(int Year, int Month)> Loaded { get; } = [];

    public int Fills { get; private set; }

    public Task<TimesheetResponse> GetTimesheetAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken)
    {
        Loaded.Add((year, month));
        return Task.FromResult(Sheet(employeeId, year, month));
    }

    public Task<TimesheetResponse> FillFromPlanAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken)
    {
        Fills++;
        for (var date = new DateOnly(year, month, 1); date.Month == month; date = date.AddDays(1))
        {
            if (Planned(date) > 0)
            {
                Days.TryAdd(date, new SaveWorkDayRequest(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, null, null));
            }
        }

        return Task.FromResult(Sheet(employeeId, year, month));
    }

    public Task<TimesheetResponse> SaveDayAsync(Guid employeeId, DateOnly day, SaveWorkDayRequest request, CancellationToken cancellationToken)
    {
        if (request.Absence is null && request.Start == request.End)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Praca musi kończyć się o innej godzinie, niż się zaczyna.", "worktime.same_times");
        }

        Days[day] = request;
        return Task.FromResult(Sheet(employeeId, day.Year, day.Month));
    }

    public Task<TimesheetResponse> ClearDayAsync(Guid employeeId, DateOnly day, CancellationToken cancellationToken)
    {
        Days.Remove(day);
        return Task.FromResult(Sheet(employeeId, day.Year, day.Month));
    }

    public Task SetClockIdAsync(Guid employeeId, SaveClockIdRequest request, CancellationToken cancellationToken)
    {
        if (request.ClockId == TakenClockId)
        {
            throw new ApiException(HttpStatusCode.Conflict, "Ten identyfikator w systemie RCP ma już inny pracownik.", "worktime.clock_id_taken");
        }

        ClockId = request.ClockId;
        return Task.CompletedTask;
    }

    public Task<ImportClockEventsResponse> ImportEventsAsync(ImportClockEventsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public List<ImportWorkDaysRequest> ImportedDays { get; } = [];

    public Task<ImportWorkDaysResponse> ImportDaysAsync(ImportWorkDaysRequest request, CancellationToken cancellationToken)
    {
        ImportedDays.Add(request);
        return Task.FromResult(new ImportWorkDaysResponse(request.Days.Count, 0, []));
    }

    private static decimal Planned(DateOnly date) => date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0m : 8m;

    private TimesheetResponse Sheet(Guid employeeId, int year, int month)
    {
        var days = new List<TimesheetDay>();
        for (var date = new DateOnly(year, month, 1); date.Month == month; date = date.AddDays(1))
        {
            var kind = date.DayOfWeek switch
            {
                DayOfWeek.Saturday => TimesheetDayKind.DayOff,
                DayOfWeek.Sunday => TimesheetDayKind.Sunday,
                _ => TimesheetDayKind.Working,
            };
            var planned = Planned(date);
            var saved = Days.GetValueOrDefault(date);
            var worked = saved is { Start: { } start, End: { } end } ? (decimal)((end - start).TotalMinutes - saved.BreakMinutes) / 60 : 0m;

            days.Add(new TimesheetDay(
                date, kind, null, true, planned, saved?.Start, saved?.End, saved?.BreakMinutes ?? 0, worked, Math.Max(0, worked - 8), 0m, 0m, 0m, 0m,
                saved?.Absence, null, saved?.Absence is null ? 0m : planned, saved?.Note, saved is not null, saved is not null, ClockIssue.None, []));
        }

        var totals = new TimesheetTotals(
            days.Sum(day => day.PlannedHours), days.Sum(day => day.PlannedHours), days.Sum(day => day.WorkedHours), days.Sum(day => day.OvertimeHours),
            0m, 0m, 0m, 0m, 0m, days.Sum(day => day.AbsenceHours), days.Count(day => day.WorkedHours > 0), 0);

        return new TimesheetResponse(employeeId, "P0001", "Ala", "Nowak", ClockId, year, month, WorkTimeSourceKind.Manual, days, totals);
    }
}
