using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.WorkTime;

/// <summary>Absences recorded in the timesheet that are not leave requests (those come from the leave module).</summary>
public enum AbsenceType
{
    /// <summary>Choroba (zwolnienie lekarskie, L4).</summary>
    Sickness = 1,

    /// <summary>Opieka nad członkiem rodziny z zasiłkiem opiekuńczym.</summary>
    CareAllowance = 2,

    /// <summary>Inna nieobecność usprawiedliwiona.</summary>
    Excused = 3,

    /// <summary>Nieobecność nieusprawiedliwiona.</summary>
    Unexcused = 4,
}

/// <summary>Where the hours of a day come from.</summary>
public enum WorkDayOrigin
{
    /// <summary>Typed or corrected by HR; events from the time clock no longer change the day.</summary>
    Manual = 1,

    /// <summary>Worked out from the clock-in and clock-out events of the day.</summary>
    Clock = 2,
}

/// <summary>What is wrong with the events of a day, so HR can correct it.</summary>
public enum ClockProblem
{
    None = 0,

    /// <summary>A clock-in without a clock-out.</summary>
    MissingOut = 1,

    /// <summary>A clock-out without a clock-in.</summary>
    MissingIn = 2,

    /// <summary>The shifts of the day run past a whole day together.</summary>
    TooLong = 3,
}

/// <summary>
/// One day of an employee's timesheet ("karta ewidencji czasu pracy"): the hours worked (from, to and the time out in
/// between) or an absence, with a note. A day with nothing recorded has no record. Night work, overtime and the norm are
/// worked out from it by <see cref="WorkTimeRules"/>.
/// </summary>
public sealed class WorkDay : Entity, ITenantOwned
{
    public const int NoteMaxLength = 200;

    /// <summary>Unpaid time out longer than this is two shifts, not a break.</summary>
    public const int MaxBreakMinutes = 8 * 60;

    // Used by EF Core.
    private WorkDay()
    {
    }

    private WorkDay(Guid tenantId, Guid employeeId, DateOnly date)
    {
        TenantId = tenantId;
        EmployeeId = employeeId;
        Date = date;
    }

    public Guid TenantId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public DateOnly Date { get; private set; }

    /// <summary>Start of work; on a day worked out from the clock with a missing clock-in, null.</summary>
    public TimeOnly? StartTime { get; private set; }

    /// <summary>End of work, on the next day when earlier than the start (a night shift).</summary>
    public TimeOnly? EndTime { get; private set; }

    /// <summary>Minutes between start and end that are not working time (going out in between).</summary>
    public int BreakMinutes { get; private set; }

    public AbsenceType? Absence { get; private set; }

    public string? Note { get; private set; }

    public WorkDayOrigin Origin { get; private set; }

    public ClockProblem Problem { get; private set; }

    public bool IsWork => Absence is null;

    /// <summary>Minutes worked: from start to end without the break; 0 for an absence or a day with a clock problem.</summary>
    public int WorkedMinutes =>
        StartTime is { } start && EndTime is { } end && Problem == ClockProblem.None ? WorkTimeRules.Span(start, end) - BreakMinutes : 0;

    public static WorkDay Worked(Guid tenantId, Guid employeeId, DateOnly date, TimeOnly start, TimeOnly end, int breakMinutes, string? note)
    {
        var day = Create(tenantId, employeeId, date);
        day.ChangeToWork(start, end, breakMinutes, note);
        return day;
    }

    public static WorkDay Absent(Guid tenantId, Guid employeeId, DateOnly date, AbsenceType absence, string? note)
    {
        var day = Create(tenantId, employeeId, date);
        day.ChangeToAbsence(absence, note);
        return day;
    }

    /// <summary>A day worked out from clock events; see <see cref="ApplyClock"/>.</summary>
    public static WorkDay FromClock(Guid tenantId, Guid employeeId, ClockDay clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var day = Create(tenantId, employeeId, clock.Date);
        day.ApplyClock(clock);
        return day;
    }

    /// <summary>HR records the hours worked; the day is manual from now on.</summary>
    public void ChangeToWork(TimeOnly start, TimeOnly end, int breakMinutes, string? note)
    {
        start = WorkTimeRules.ToMinute(start);
        end = WorkTimeRules.ToMinute(end);

        if (start == end)
        {
            throw new DomainException("Work must end at a different time than it starts.");
        }

        if (breakMinutes < 0 || breakMinutes > MaxBreakMinutes || breakMinutes >= WorkTimeRules.Span(start, end))
        {
            throw new DomainException("The break must be shorter than the time between start and end.");
        }

        StartTime = start;
        EndTime = end;
        BreakMinutes = breakMinutes;
        Absence = null;
        Note = Clean(note);
        Origin = WorkDayOrigin.Manual;
        Problem = ClockProblem.None;
    }

    /// <summary>HR records an absence; the day is manual from now on.</summary>
    public void ChangeToAbsence(AbsenceType absence, string? note)
    {
        if (!Enum.IsDefined(absence))
        {
            throw new DomainException("Unknown kind of absence.");
        }

        StartTime = null;
        EndTime = null;
        BreakMinutes = 0;
        Absence = absence;
        Note = Clean(note);
        Origin = WorkDayOrigin.Manual;
        Problem = ClockProblem.None;
    }

    /// <summary>Takes the hours from the clock events. A day HR typed or corrected keeps its hours.</summary>
    public void ApplyClock(ClockDay clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        if (Origin == WorkDayOrigin.Manual)
        {
            throw new DomainException("A day recorded by hand is not changed by clock events.");
        }

        if (clock.Date != Date)
        {
            throw new DomainException("Clock events are for another day.");
        }

        StartTime = clock.Start;
        EndTime = clock.End;
        BreakMinutes = clock.BreakMinutes;
        Absence = null;
        Origin = WorkDayOrigin.Clock;
        Problem = clock.Problem;
    }

    private static WorkDay Create(Guid tenantId, Guid employeeId, DateOnly date)
    {
        if (tenantId == Guid.Empty || employeeId == Guid.Empty)
        {
            throw new DomainException("A working day must belong to a tenant and an employee.");
        }

        return new WorkDay(tenantId, employeeId, date);
    }

    private static string? Clean(string? note)
    {
        var trimmed = note?.Trim();
        if (trimmed?.Length > NoteMaxLength)
        {
            throw new DomainException($"Note cannot exceed {NoteMaxLength} characters.");
        }

        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
