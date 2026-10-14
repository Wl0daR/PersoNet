using PersoNet.Domain.Leave;

namespace PersoNet.Domain.WorkTime;

/// <summary>What kind of day a date is in the basic working time system (Monday to Friday, 8 hours a day).</summary>
public enum DayKind
{
    /// <summary>Monday to Friday that is not a public holiday.</summary>
    Working = 1,

    /// <summary>Saturday: the day off of the five-day week.</summary>
    DayOff = 2,

    Sunday = 3,

    /// <summary>A public holiday on a day other than Sunday.</summary>
    Holiday = 4,
}

/// <summary>The figures of one day of a timesheet, in minutes.</summary>
/// <param name="Kind">The kind of day.</param>
/// <param name="Planned">Hours the employee should work that day: the working day times the working time of the contract.</param>
/// <param name="Worked">Minutes worked.</param>
/// <param name="Overtime">Hours over the 8-hour daily norm on a working day (praca w godzinach nadliczbowych).</param>
/// <param name="OverPlan">
/// For part-time employees: hours over their planned day but within 8 hours (godziny ponadwymiarowe, art. 151 § 5).
/// </param>
/// <param name="OnDayOff">Hours worked on a Saturday, the day off of the five-day week.</param>
/// <param name="OnSundayOrHoliday">Hours worked on a Sunday or a public holiday.</param>
/// <param name="Night">Hours worked in the night period.</param>
public sealed record DayFigures(DayKind Kind, int Planned, int Worked, int Overtime, int OverPlan, int OnDayOff, int OnSundayOrHoliday, int Night);

/// <summary>
/// Labour Code rules for the timesheet in the basic working time system: 8 hours a day, five days a week (art. 129),
/// the working time norm of a period (art. 130) and night work (art. 151⁷). Overtime is counted per day; how it is paid
/// (50 or 100 percent, or a day off) is decided in payroll.
/// </summary>
public static class WorkTimeRules
{
    public const int MinutesPerDay = 24 * 60;

    /// <summary>The daily norm of a full-time employee: 8 hours.</summary>
    public const int DailyNormMinutes = 8 * 60;

    /// <summary>The night period: eight hours from 22:00 to 6:00 (the company may set another between 21:00 and 7:00).</summary>
    public static readonly TimeOnly NightStart = new(22, 0);

    public static readonly TimeOnly NightEnd = new(6, 0);

    public static DayKind Classify(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Sunday => DayKind.Sunday,
        _ when PolishCalendar.IsPublicHoliday(date) => DayKind.Holiday,
        DayOfWeek.Saturday => DayKind.DayOff,
        _ => DayKind.Working,
    };

    /// <summary>The time without seconds: working time is counted in whole minutes.</summary>
    public static TimeOnly ToMinute(TimeOnly time) => new(time.Hour, time.Minute);

    public static DateTime ToMinute(DateTime time) => new(time.Year, time.Month, time.Day, time.Hour, time.Minute, 0, DateTimeKind.Unspecified);

    /// <summary>Minutes from <paramref name="start"/> to <paramref name="end"/>, past midnight when the end is earlier.</summary>
    public static int Span(TimeOnly start, TimeOnly end) => (int)(end - start).TotalMinutes;

    /// <summary>
    /// What a day adds to the working time norm (art. 130): 8 hours for each day from Monday to Friday, less 8 hours for a
    /// public holiday that falls on any day other than Sunday. So a holiday on a Saturday lowers the norm, and the employee
    /// gets another day off. Part-time: in proportion. Days without an employment contract add nothing.
    /// </summary>
    public static int NormMinutes(DateOnly date, decimal? workingTime)
    {
        if (workingTime is not { } fraction)
        {
            return 0;
        }

        var weekday = date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) ? DailyNormMinutes : 0;
        var holiday = date.DayOfWeek != DayOfWeek.Sunday && PolishCalendar.IsPublicHoliday(date) ? DailyNormMinutes : 0;
        return (int)Math.Round((weekday - holiday) * fraction, MidpointRounding.AwayFromZero);
    }

    /// <summary>The hours planned for a day: a working day times the working time of the contract, nothing on other days.</summary>
    public static int PlannedMinutes(DateOnly date, decimal? workingTime) =>
        Classify(date) == DayKind.Working && workingTime is { } fraction
            ? (int)Math.Round(DailyNormMinutes * fraction, MidpointRounding.AwayFromZero)
            : 0;

    /// <summary>
    /// Minutes of work in the night period. The break is assumed to fall outside it, so the result is at most the minutes
    /// worked.
    /// </summary>
    public static int NightMinutes(TimeOnly start, TimeOnly end, int workedMinutes)
    {
        var from = (int)start.ToTimeSpan().TotalMinutes;
        var to = from + Span(start, end);
        var nightFrom = (int)NightStart.ToTimeSpan().TotalMinutes;
        var nightTo = (int)NightEnd.ToTimeSpan().TotalMinutes;

        // The night periods that can meet a shift starting today: last night, tonight and the next one.
        var night = Overlap(from, to, nightFrom - MinutesPerDay, nightTo)
            + Overlap(from, to, nightFrom, nightTo + MinutesPerDay)
            + Overlap(from, to, nightFrom + MinutesPerDay, nightTo + (2 * MinutesPerDay));

        return Math.Min(night, workedMinutes);
    }

    public static DayFigures Figures(DateOnly date, decimal? workingTime, int workedMinutes, int nightMinutes)
    {
        var kind = Classify(date);
        var planned = PlannedMinutes(date, workingTime);
        var working = kind == DayKind.Working;

        return new DayFigures(
            kind,
            planned,
            workedMinutes,
            working ? Math.Max(0, workedMinutes - DailyNormMinutes) : 0,
            working && workingTime is not null ? Math.Max(0, Math.Min(workedMinutes, DailyNormMinutes) - planned) : 0,
            kind == DayKind.DayOff ? workedMinutes : 0,
            kind is DayKind.Sunday or DayKind.Holiday ? workedMinutes : 0,
            nightMinutes);
    }

    private static int Overlap(int from, int to, int otherFrom, int otherTo) => Math.Max(0, Math.Min(to, otherTo) - Math.Max(from, otherFrom));
}
