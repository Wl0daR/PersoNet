using System.Globalization;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>A leave kind with its Polish name, for the drop-down list.</summary>
public sealed record LeaveKindChoice(LeaveKind Kind, string Name);

/// <summary>A filter of the request list; <see cref="State"/> null means all.</summary>
public sealed record LeaveStateChoice(LeaveState? State, string Name);

/// <summary>One request as shown in a list, with its texts ready for display.</summary>
public sealed record LeaveRow(LeaveRequestResponse Request, string KindName, string Period, string DaysText, string StateName)
{
    public bool IsPending => Request.State == LeaveState.Pending;

    public bool IsApproved => Request.State == LeaveState.Approved;

    public string EmployeeInitials => Initials.Of(Request.EmployeeName);

    /// <summary>"7 dni · 56 godz." for whole days, "2,5 godz." for leave in hours.</summary>
    public string ExtentText => Request.IsHourly ? DaysText : $"{DaysText} · {LeaveTexts.Hours(Request.Hours)}";

    /// <summary>"zatwierdzony 20 maja 2025" once decided, otherwise empty.</summary>
    public string DecisionText => Request.DecidedAt is { } at
        ? $"{StateName} {at.ToLocalTime().ToString("d MMMM yyyy", CultureInfo.GetCultureInfo("pl-PL"))}"
        : string.Empty;
}

/// <summary>Polish names and formats shared by the leave screens.</summary>
public static class LeaveTexts
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static IReadOnlyList<LeaveKindChoice> Kinds { get; } =
    [
        new(LeaveKind.Annual, "Urlop wypoczynkowy"),
        new(LeaveKind.OnDemand, "Urlop na żądanie"),
        new(LeaveKind.Occasional, "Urlop okolicznościowy"),
        new(LeaveKind.Unpaid, "Urlop bezpłatny"),
        new(LeaveKind.ChildCare, "Opieka nad dzieckiem (art. 188)"),
        new(LeaveKind.ForceMajeure, "Siła wyższa (art. 148¹)"),
    ];

    /// <summary>Kinds that may be taken in hours (the Labour Code rules are checked by the server).</summary>
    public static bool AllowsHours(LeaveKind kind) => kind is LeaveKind.Annual or LeaveKind.ChildCare or LeaveKind.ForceMajeure;

    public static string KindName(LeaveKind kind) => Kinds.FirstOrDefault(choice => choice.Kind == kind)?.Name ?? kind.ToString();

    public static string StateName(LeaveState state) => state switch
    {
        LeaveState.Pending => "oczekuje",
        LeaveState.Approved => "zatwierdzony",
        LeaveState.Rejected => "odrzucony",
        LeaveState.Cancelled => "anulowany",
        _ => state.ToString(),
    };

    /// <summary>"3 mar 2025" for one day, "28 kwi – 6 maj 2025" for a period.</summary>
    public static string Period(DateOnly start, DateOnly end) => start == end
        ? start.ToString("d MMM yyyy", Polish)
        : $"{start.ToString("d MMM", Polish)} – {end.ToString("d MMM yyyy", Polish)}";

    /// <summary>"1 dzień", "2 dni", "5 dni".</summary>
    public static string Days(int days) => days == 1 ? "1 dzień" : $"{days} dni";

    /// <summary>"2,5 godz."</summary>
    public static string Hours(decimal hours) => $"{hours.ToString("0.##", Polish)} godz.";

    /// <summary>
    /// Leave time in the Labour Code's days of 8 hours: "13 dni", "1 dzień 4 godz.", "6 godz.". Negative amounts are shown as 0.
    /// </summary>
    public static string Amount(decimal hours)
    {
        hours = Math.Max(0, hours);
        var days = (int)decimal.Truncate(hours / HoursPerDay);
        var rest = hours - (days * HoursPerDay);

        return (days, rest) switch
        {
            (0, _) => Hours(rest),
            (_, 0) => Days(days),
            _ => $"{Days(days)} {Hours(rest)}",
        };
    }

    /// <summary>One day of leave entitlement is 8 hours (art. 154² KP).</summary>
    public const int HoursPerDay = 8;

    /// <summary>Reads hours typed as "2", "2,5", "2.5" or "2:30"; null when it is not a number of hours.</summary>
    public static decimal? ParseHours(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var value = text.Trim();
        var colon = value.IndexOf(':', StringComparison.Ordinal);
        if (colon > 0)
        {
            return int.TryParse(value[..colon], NumberStyles.None, CultureInfo.InvariantCulture, out var whole)
                && int.TryParse(value[(colon + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes < 60
                ? whole + Math.Round(minutes / 60m, 2)
                : null;
        }

        return decimal.TryParse(value.Replace(',', '.'), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var hours) ? hours : null;
    }

    public static LeaveRow ToRow(LeaveRequestResponse request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var amount = request.IsHourly ? Hours(request.Hours) : Days(request.Days);
        return new LeaveRow(request, KindName(request.Kind), Period(request.StartDate, request.EndDate), amount, StateName(request.State));
    }
}
