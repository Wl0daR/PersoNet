using System.Globalization;
using PersoNet.Contracts.Leave;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>
/// One day in the month calendar of an employee: the date, whether it is a day off (weekend or public holiday) and the
/// leave on it, if any. Cancelled and rejected requests are not shown.
/// </summary>
public sealed record CalendarDay(DateOnly Date, bool IsInMonth, bool IsToday, string? HolidayName, LeaveRow? Leave, bool IsInDraft)
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public int Day => Date.Day;

    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public bool IsHoliday => HolidayName is not null;

    /// <summary>Weekend or public holiday: no work, so no leave is counted.</summary>
    public bool IsDayOff => IsWeekend || HolidayName is not null;

    public bool HasLeave => Leave is not null;

    public bool IsApproved => Leave?.IsApproved == true;

    public bool IsPending => Leave?.IsPending == true;

    /// <summary>Leave taken in hours on this day, marked in the cell so it is not mistaken for a whole day.</summary>
    public bool IsHourly => Leave?.Request.IsHourly == true;

    /// <summary>"środa, 14 października 2026".</summary>
    public string LongDate => Date.ToString("dddd, d MMMM yyyy", Polish);

    /// <summary>What the day is, in words: for the tooltip, screen readers and the details under the calendar.</summary>
    public string Description => Leave is { } leave
        ? $"{leave.KindName}, {leave.StateName}" + (IsHourly ? $" ({leave.DaysText})" : string.Empty)
        : HolidayName is { } holiday
            ? $"Święto: {holiday}"
            : IsWeekend ? "Weekend" : "Dzień roboczy bez urlopu";

    public string AutomationName => $"{LongDate}: {Description}";
}

/// <summary>Builds the 6-week grid (Monday first) that shows a month.</summary>
public static class LeaveCalendar
{
    public const int Weeks = 6;

    /// <summary>Short Polish weekday names, Monday first, for the calendar header.</summary>
    public static IReadOnlyList<string> WeekdayNames { get; } = ["Pn", "Wt", "Śr", "Cz", "Pt", "So", "Nd"];

    /// <summary>"Październik 2026".</summary>
    public static string MonthTitle(DateOnly month)
    {
        var text = month.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("pl-PL"));
        return char.ToUpper(text[0], CultureInfo.GetCultureInfo("pl-PL")) + text[1..];
    }

    /// <summary>
    /// Days from the Monday on or before the 1st of <paramref name="month"/>, six weeks long. <paramref name="draft"/> is
    /// the period of a request being filled in, highlighted in the grid.
    /// </summary>
    public static IReadOnlyList<CalendarDay> Build(
        DateOnly month,
        DateOnly today,
        IReadOnlyDictionary<DateOnly, string> holidays,
        IEnumerable<LeaveRow> requests,
        (DateOnly Start, DateOnly End)? draft)
    {
        ArgumentNullException.ThrowIfNull(holidays);
        ArgumentNullException.ThrowIfNull(requests);

        var first = new DateOnly(month.Year, month.Month, 1);
        var start = first.AddDays(-(((int)first.DayOfWeek + 6) % 7));
        var active = requests.Where(row => row.Request.State is LeaveState.Pending or LeaveState.Approved).ToList();

        return Enumerable.Range(0, Weeks * 7)
            .Select(offset => start.AddDays(offset))
            .Select(date => new CalendarDay(
                date,
                date.Month == month.Month,
                date == today,
                holidays.GetValueOrDefault(date),
                active.FirstOrDefault(row => row.Request.StartDate <= date && date <= row.Request.EndDate),
                draft is { } period && period.Start <= date && date <= period.End))
            .ToList();
    }
}
