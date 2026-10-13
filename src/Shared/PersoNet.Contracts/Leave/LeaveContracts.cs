using System.Text.Json.Serialization;

namespace PersoNet.Contracts.Leave;

/// <summary>Kind of leave; sent as text, e.g. "Annual".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LeaveKind>))]
public enum LeaveKind
{
    /// <summary>Urlop wypoczynkowy.</summary>
    Annual = 1,

    /// <summary>Urlop na żądanie (part of annual leave, at most 4 days a year).</summary>
    OnDemand = 2,

    /// <summary>Urlop okolicznościowy.</summary>
    Occasional = 3,

    /// <summary>Urlop bezpłatny.</summary>
    Unpaid = 4,

    /// <summary>Opieka nad dzieckiem (art. 188 KP, at most 2 days a year).</summary>
    ChildCare = 5,
}

/// <summary>Where a request stands; sent as text, e.g. "Pending".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<LeaveState>))]
public enum LeaveState
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

/// <summary>A leave request with the employee's name resolved. <see cref="Days"/> are working days.</summary>
public sealed record LeaveRequestResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    LeaveKind Kind,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days,
    string? Note,
    LeaveState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt,
    string? DecisionNote);

public sealed record CreateLeaveRequest(Guid EmployeeId, LeaveKind Kind, DateOnly StartDate, DateOnly EndDate, string? Note);

/// <summary>Approve, reject or cancel. The note is required when rejecting.</summary>
public sealed record LeaveDecisionRequest(string? Note);

/// <summary>
/// Annual leave of one employee in one year. Used and pending count annual and on-demand leave; remaining is what can
/// still be requested.
/// </summary>
public sealed record LeaveBalanceResponse(
    Guid EmployeeId,
    int Year,
    bool HasEntitlement,
    int EntitlementDays,
    int CarriedOverDays,
    int UsedDays,
    int PendingDays,
    int RemainingDays,
    int OnDemandDays);

public sealed record SaveLeaveEntitlementRequest(int Days, int CarriedOverDays);
