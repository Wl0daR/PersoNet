using PersoNet.Contracts.Leave;

namespace PersoNet.Web.Portal;

/// <summary>
/// A month of the team calendar laid out for display: its days (weekends and public holidays marked) and, per person,
/// what they have on each day. Approved leave wins over a request still waiting when both fall on one day.
/// </summary>
internal sealed class LeaveCalendarMonth
{
    private static readonly string[] DayNames = ["nd", "pn", "wt", "śr", "cz", "pt", "sb"];

    public LeaveCalendarMonth(LeaveCalendarResponse calendar, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var first = new DateOnly(calendar.Year, calendar.Month, 1);
        var holidays = calendar.Holidays.ToDictionary(holiday => holiday.Date, holiday => holiday.Name);

        Title = first.ToString("MMMM yyyy", LeaveTexts.Polish);
        Days = Enumerable.Range(0, DateTime.DaysInMonth(calendar.Year, calendar.Month))
            .Select(offset => first.AddDays(offset))
            .Select(date => new CalendarDay(date, DayNames[(int)date.DayOfWeek], holidays.GetValueOrDefault(date), date == today))
            .ToList();
        Rows = calendar.People
            .Select(person => new CalendarRow(person, Days.Select(day => Cell(person, day)).ToList()))
            .ToList();
        Absences = calendar.People
            .SelectMany(person => person.Leave.Select(entry => (Person: person, Entry: entry)))
            .OrderBy(item => item.Entry.StartDate)
            .ThenBy(item => item.Person.LastName, StringComparer.Create(LeaveTexts.Polish, ignoreCase: true))
            .ToList();
    }

    /// <summary>"czerwiec 2025".</summary>
    public string Title { get; }

    public IReadOnlyList<CalendarDay> Days { get; }

    public IReadOnlyList<CalendarRow> Rows { get; }

    /// <summary>Every leave in the month, earliest first: the same information as the grid, readable as a list.</summary>
    public IReadOnlyList<(LeaveCalendarPerson Person, LeaveCalendarEntry Entry)> Absences { get; }

    /// <summary>People away on the given day (approved leave only).</summary>
    public int AwayOn(DateOnly date) =>
        Rows.Count(row => row.Cells.Any(cell => cell.Day.Date == date && cell.Entry is { State: LeaveState.Approved }));

    /// <summary>Short mark of a kind of leave, so the grid does not rely on colour: W, Ż, O, B, D, S and the rest.</summary>
    public static string Mark(LeaveKind kind) => kind switch
    {
        LeaveKind.Annual => "W",
        LeaveKind.OnDemand => "Ż",
        LeaveKind.Occasional => "O",
        LeaveKind.Unpaid => "B",
        LeaveKind.ChildCare => "D",
        LeaveKind.ForceMajeure => "S",
        LeaveKind.Maternity => "M",
        LeaveKind.Paternity => "T",
        LeaveKind.Parental => "R",
        LeaveKind.ChildRearing => "Y",
        LeaveKind.OvertimeTimeOff => "N",
        LeaveKind.HolidayTimeOff => "Ś",
        LeaveKind.PaidRelease => "P",
        _ => "?",
    };

    private static CalendarCell Cell(LeaveCalendarPerson person, CalendarDay day)
    {
        var entry = person.Leave
            .Where(entry => entry.StartDate <= day.Date && entry.EndDate >= day.Date)
            .OrderBy(entry => entry.State == LeaveState.Approved ? 0 : 1)
            .FirstOrDefault();

        // Leave counts working days only, so a weekend or holiday inside it is not marked as taken.
        return new CalendarCell(day, day.IsDayOff ? null : entry);
    }
}

/// <summary>A day of the month; <see cref="Holiday"/> is the name of a public holiday.</summary>
internal sealed record CalendarDay(DateOnly Date, string DayName, string? Holiday, bool IsToday)
{
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public bool IsDayOff => IsWeekend || Holiday is not null;
}

/// <summary>A person's row: one cell per day.</summary>
internal sealed record CalendarRow(LeaveCalendarPerson Person, IReadOnlyList<CalendarCell> Cells);

/// <summary>What the person has on the day, or nothing.</summary>
internal sealed record CalendarCell(CalendarDay Day, LeaveCalendarEntry? Entry)
{
    /// <summary>Read by screen readers and shown on hover: "2 czerwca: urlop wypoczynkowy, czeka na decyzję".</summary>
    public string Description
    {
        get
        {
            var date = Day.Date.ToString("d MMMM", LeaveTexts.Polish);
            if (Entry is { } entry)
            {
                var hours = entry.IsHourly ? $" ({LeaveTexts.Hours(entry.Hours)})" : string.Empty;
                var state = entry.State == LeaveState.Pending ? ", czeka na decyzję" : string.Empty;
                return $"{date}: {LeaveTexts.KindName(entry.Kind).ToLowerInvariant()}{hours}{state}";
            }

            return Day.Holiday is { } holiday ? $"{date}: {holiday}" : Day.IsWeekend ? $"{date}: weekend" : $"{date}: w pracy";
        }
    }
}
