namespace PersoNet.Domain.WorkTime;

/// <summary>A day of work as the clock events show it: first in, last out, the time out in between, and any problem.</summary>
public sealed record ClockDay(DateOnly Date, TimeOnly? Start, TimeOnly? End, int BreakMinutes, ClockProblem Problem);

/// <summary>
/// Turns clock-in and clock-out events into working days. Each clock-in is paired with the next clock-out; a shift belongs
/// to the day it started, so a night shift ending after midnight stays one day. A repeated tap of the card within a few
/// minutes counts once. A clock-in without a clock-out (or the reverse) marks its day for HR to correct.
/// </summary>
public static class ClockPairing
{
    /// <summary>A clock-out later than this after the clock-in is not the end of the same shift.</summary>
    public static readonly TimeSpan MaxShift = TimeSpan.FromHours(16);

    /// <summary>Events of the same direction this close together are one tap of the card.</summary>
    public static readonly TimeSpan RepeatedTap = TimeSpan.FromMinutes(2);

    public static IReadOnlyList<ClockDay> Pair(IEnumerable<(DateTime At, ClockDirection Direction)> events)
    {
        ArgumentNullException.ThrowIfNull(events);

        var shifts = new List<(DateTime? In, DateTime? Out)>();
        DateTime? open = null;

        // Working time is counted in whole minutes.
        foreach (var (at, direction) in events.Select(e => (At: WorkTimeRules.ToMinute(e.At), e.Direction)).OrderBy(e => e.At).ThenBy(e => e.Direction))
        {
            if (direction == ClockDirection.In)
            {
                if (open is { } earlier)
                {
                    if (at - earlier < RepeatedTap)
                    {
                        continue;
                    }

                    shifts.Add((earlier, null));
                }

                open = at;
                continue;
            }

            if (open is { } start)
            {
                if (at - start <= MaxShift)
                {
                    shifts.Add((start, at));
                }
                else
                {
                    shifts.Add((start, null));
                    shifts.Add((null, at));
                }

                open = null;
            }
            else if (shifts.Count == 0 || shifts[^1].Out is not { } lastOut || at - lastOut >= RepeatedTap)
            {
                shifts.Add((null, at));
            }
        }

        if (open is { } unfinished)
        {
            shifts.Add((unfinished, null));
        }

        return shifts
            .GroupBy(shift => DateOnly.FromDateTime((shift.In ?? shift.Out)!.Value))
            .OrderBy(day => day.Key)
            .Select(day => ToDay(day.Key, day.ToList()))
            .ToList();
    }

    private static ClockDay ToDay(DateOnly date, List<(DateTime? In, DateTime? Out)> shifts)
    {
        var first = shifts.Where(shift => shift.In is not null).Select(shift => shift.In!.Value).DefaultIfEmpty().Min();
        var last = shifts.Where(shift => shift.Out is not null).Select(shift => shift.Out!.Value).DefaultIfEmpty().Max();
        TimeOnly? start = first == default ? null : TimeOnly.FromDateTime(first);
        TimeOnly? end = last == default ? null : TimeOnly.FromDateTime(last);

        if (shifts.Any(shift => shift.Out is null))
        {
            return new ClockDay(date, start, end, 0, ClockProblem.MissingOut);
        }

        if (shifts.Any(shift => shift.In is null))
        {
            return new ClockDay(date, start, end, 0, ClockProblem.MissingIn);
        }

        var worked = shifts.Sum(shift => Minutes(shift.Out!.Value - shift.In!.Value));
        var span = Minutes(last - first);

        // Shifts of one day that together run past a whole day cannot be shown as one from-to; HR splits them.
        return span >= WorkTimeRules.MinutesPerDay || start == end
            ? new ClockDay(date, start, end, 0, ClockProblem.TooLong)
            : new ClockDay(date, start, end, span - worked, ClockProblem.None);
    }

    private static int Minutes(TimeSpan time) => (int)time.TotalMinutes;
}
