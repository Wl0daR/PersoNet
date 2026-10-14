using System.Globalization;
using PersoNet.Contracts.WorkTime;

namespace PersoNet.Desktop.Presentation.WorkTime;

/// <summary>A way of recording working time, as HR picks it in the company settings.</summary>
public sealed record WorkTimeSourceChoice(WorkTimeSourceKind Kind, string Name, string Hint);

/// <summary>A kind of absence for the day dialog.</summary>
public sealed record AbsenceChoice(AbsenceKind Kind, string Name);

/// <summary>Polish names and formats shared by the working time screens.</summary>
public static class WorkTimeTexts
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static IReadOnlyList<WorkTimeSourceChoice> Sources { get; } =
    [
        new(WorkTimeSourceKind.Manual, "Wpis ręczny",
            "Kadry wpisują godziny w karcie pracownika albo wypełniają miesiąc według planu i poprawiają wyjątki."),
        new(WorkTimeSourceKind.FileImport, "Import plików z systemu RCP",
            "Kadry wczytują pliki z odbiciami wyeksportowane z systemu RCP (CSV, TXT, Excel). Wczytywanie plików pojawi się w kolejnej wersji."),
        new(WorkTimeSourceKind.RogerPrMaster, "Roger PR Master",
            "Odbicia z Roger PR Master trafiają do PersoNet same, z folderu, do którego PR Master zapisuje zdarzenia. Połączenie pojawi się w kolejnej wersji."),
        new(WorkTimeSourceKind.Api, "Inny system RCP przez API",
            "System RCP wysyła odbicia do PersoNet przez API (POST /api/v1/clock-events/import), a pracowników rozpoznaje po identyfikatorze RCP."),
    ];

    public static IReadOnlyList<AbsenceChoice> Absences { get; } =
    [
        new(AbsenceKind.Sickness, "Choroba (L4)"),
        new(AbsenceKind.CareAllowance, "Opieka (zasiłek opiekuńczy)"),
        new(AbsenceKind.Excused, "Nieobecność usprawiedliwiona"),
        new(AbsenceKind.Unexcused, "Nieobecność nieusprawiedliwiona"),
    ];

    public static string SourceName(WorkTimeSourceKind kind) => Sources.FirstOrDefault(source => source.Kind == kind)?.Name ?? kind.ToString();

    public static string AbsenceName(AbsenceKind kind) => Absences.FirstOrDefault(absence => absence.Kind == kind)?.Name ?? kind.ToString();

    public static string IssueText(ClockIssue issue) => issue switch
    {
        ClockIssue.MissingOut => "brak wyjścia",
        ClockIssue.MissingIn => "brak wejścia",
        ClockIssue.TooLong => "ponad dobę między odbiciami",
        _ => string.Empty,
    };

    /// <summary>Hours as on a timesheet: 8:10 for 8 hours 10 minutes; empty for none.</summary>
    public static string Duration(decimal hours)
    {
        if (hours == 0)
        {
            return string.Empty;
        }

        var minutes = (int)Math.Round(hours * 60, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{minutes / 60}:{minutes % 60:00}");
    }

    /// <summary>A total, which shows 0:00 rather than nothing.</summary>
    public static string Total(decimal hours) => hours == 0 ? "0:00" : Duration(hours);

    public static string Time(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    /// <summary>Short weekday and day of month, e.g. "pn 3".</summary>
    public static string ShortDay(DateOnly date) => date.ToString("ddd d", Polish).Replace(".", string.Empty, StringComparison.Ordinal);

    public static string LongDay(DateOnly date) => date.ToString("dddd, d MMMM yyyy", Polish);

    /// <summary>
    /// Reads a time as people type it: "8", "8:30", "08.30", "830" or "0830". Null when it is not a time of day.
    /// </summary>
    public static TimeOnly? ParseTime(string? text)
    {
        var value = text?.Trim().Replace('.', ':') ?? string.Empty;
        if (value.Length == 0)
        {
            return null;
        }

        if (!value.Contains(':', StringComparison.Ordinal))
        {
            value = value.Length <= 2 ? value + ":00" : value.Length <= 4 ? value[..^2] + ":" + value[^2..] : value;
        }

        var parts = value.Split(':');
        return parts.Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hour)
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minute)
            && parts[1].Length == 2 && hour is >= 0 and <= 23 && minute is >= 0 and <= 59
                ? new TimeOnly(hour, minute)
                : null;
    }
}
