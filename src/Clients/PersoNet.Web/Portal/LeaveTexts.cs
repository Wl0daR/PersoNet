using System.Globalization;
using PersoNet.Contracts.Leave;

namespace PersoNet.Web.Portal;

/// <summary>Polish names and formats of leave in the portal (the same wording as the desktop app).</summary>
internal static class LeaveTexts
{
    /// <summary>One day of leave entitlement is 8 hours (art. 154² KP).</summary>
    public const int HoursPerDay = 8;

    public static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static IReadOnlyList<(LeaveKind Kind, string Name)> Kinds { get; } =
    [
        (LeaveKind.Annual, "Urlop wypoczynkowy"),
        (LeaveKind.OnDemand, "Urlop na żądanie"),
        (LeaveKind.Occasional, "Urlop okolicznościowy"),
        (LeaveKind.Unpaid, "Urlop bezpłatny"),
        (LeaveKind.ChildCare, "Opieka nad dzieckiem (art. 188)"),
        (LeaveKind.ForceMajeure, "Siła wyższa (art. 148¹)"),
    ];

    /// <summary>Kinds that may be taken in hours (the Labour Code rules are checked by the server).</summary>
    public static bool AllowsHours(LeaveKind kind) => kind is LeaveKind.Annual or LeaveKind.ChildCare or LeaveKind.ForceMajeure;

    public static string KindName(LeaveKind kind) => Kinds.FirstOrDefault(choice => choice.Kind == kind).Name ?? kind.ToString();

    public static string StateName(LeaveState state) => state switch
    {
        LeaveState.Pending => "Czeka na decyzję",
        LeaveState.Approved => "Zatwierdzony",
        LeaveState.Rejected => "Odrzucony",
        LeaveState.Cancelled => "Anulowany",
        _ => state.ToString(),
    };

    /// <summary>"3 mar 2026" for one day, "28 kwi – 6 maj 2026" for a period.</summary>
    public static string Period(DateOnly start, DateOnly end) => start == end
        ? start.ToString("d MMM yyyy", Polish)
        : $"{start.ToString("d MMM", Polish)} – {end.ToString("d MMM yyyy", Polish)}";

    public static string Date(DateOnly date) => date.ToString("d MMMM yyyy", Polish);

    /// <summary>The Polish plural form for <paramref name="count"/>: one (1), few (2–4, 22–24…) or many (5–21, 25…).</summary>
    public static string Plural(int count, string one, string few, string many)
    {
        var lastTwo = Math.Abs(count) % 100;
        var last = lastTwo % 10;
        return count == 1 ? one : last is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? few : many;
    }

    /// <summary>"1 dzień", "2 dni", "5 dni".</summary>
    public static string Days(int days) => days == 1 ? "1 dzień" : $"{days} dni";

    /// <summary>"2,5 godz."</summary>
    public static string Hours(decimal hours) => $"{hours.ToString("0.##", Polish)} godz.";

    /// <summary>How long a request is: "5 dni" or "2,5 godz." for leave in hours.</summary>
    public static string Extent(LeaveRequestResponse request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return request.IsHourly ? Hours(request.Hours) : Days(request.Days);
    }

    /// <summary>Leave time in the Labour Code's days of 8 hours: "13 dni", "1 dzień 4 godz.", "6 godz.", "0 dni".</summary>
    public static string Amount(decimal hours)
    {
        hours = Math.Max(0, hours);
        var days = (int)decimal.Truncate(hours / HoursPerDay);
        var rest = hours - (days * HoursPerDay);

        return (days, rest) switch
        {
            (0, 0) => "0 dni",
            (0, _) => Hours(rest),
            (_, 0) => Days(days),
            _ => $"{Days(days)} {Hours(rest)}",
        };
    }

    /// <summary>Reads hours typed as "2", "2,5", "2.5" or "2:30"; null when it is not a number of hours.</summary>
    public static decimal? ParseHours(string? text)
    {
        var value = text?.Trim() ?? string.Empty;
        var colon = value.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            return int.TryParse(value[..colon], NumberStyles.None, CultureInfo.InvariantCulture, out var whole)
                && int.TryParse(value[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes < 60
                ? whole + Math.Round(minutes / 60m, 2)
                : null;
        }

        return decimal.TryParse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hours) && hours > 0
            ? hours
            : null;
    }
}
