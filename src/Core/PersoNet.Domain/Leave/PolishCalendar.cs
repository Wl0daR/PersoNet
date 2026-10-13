namespace PersoNet.Domain.Leave;

/// <summary>
/// Public holidays in Poland (ustawa o dniach wolnych od pracy) and working days for leave: Monday to Friday that are not
/// a public holiday. Saturdays off "for a holiday falling on Saturday" are a company decision and are not included.
/// </summary>
public static class PolishCalendar
{
    /// <summary>Christmas Eve is a public holiday from 2025 on.</summary>
    public const int ChristmasEveFromYear = 2025;

    public static bool IsPublicHoliday(DateOnly date)
    {
        var easter = EasterSunday(date.Year);

        return (date.Month, date.Day) switch
        {
            (1, 1) or (1, 6) or (5, 1) or (5, 3) or (8, 15) or (11, 1) or (11, 11) or (12, 25) or (12, 26) => true,
            (12, 24) => date.Year >= ChristmasEveFromYear,
            _ => date == easter
                || date == easter.AddDays(1) // Easter Monday
                || date == easter.AddDays(49) // Pentecost (always a Sunday)
                || date == easter.AddDays(60), // Corpus Christi
        };
    }

    public static bool IsWorkingDay(DateOnly date) =>
        date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsPublicHoliday(date);

    /// <summary>Working days from <paramref name="start"/> to <paramref name="end"/>, both inclusive.</summary>
    public static int WorkingDays(DateOnly start, DateOnly end)
    {
        var count = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (IsWorkingDay(day))
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Easter Sunday in the Gregorian calendar (anonymous Gregorian algorithm).</summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = ((19 * a) + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + (2 * e) + (2 * i) - h - k) % 7;
        var m = (a + (11 * h) + (22 * l)) / 451;
        var month = (h + l - (7 * m) + 114) / 31;
        var day = ((h + l - (7 * m) + 114) % 31) + 1;

        return new DateOnly(year, month, day);
    }
}
