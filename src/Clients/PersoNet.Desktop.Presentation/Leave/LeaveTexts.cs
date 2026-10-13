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
    ];

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

    public static LeaveRow ToRow(LeaveRequestResponse request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new LeaveRow(request, KindName(request.Kind), Period(request.StartDate, request.EndDate), Days(request.Days), StateName(request.State));
    }
}
