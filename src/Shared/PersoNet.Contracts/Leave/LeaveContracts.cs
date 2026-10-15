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

    /// <summary>Opieka nad dzieckiem (art. 188 KP): 2 days or 16 hours a year.</summary>
    ChildCare = 5,

    /// <summary>Zwolnienie z powodu siły wyższej (art. 148¹ KP): 2 days or 16 hours a year.</summary>
    ForceMajeure = 6,

    /// <summary>Urlop macierzyński.</summary>
    Maternity = 7,

    /// <summary>Urlop ojcowski.</summary>
    Paternity = 8,

    /// <summary>Urlop rodzicielski.</summary>
    Parental = 9,

    /// <summary>Urlop wychowawczy.</summary>
    ChildRearing = 10,

    /// <summary>Odbiór nadgodzin (czas wolny za pracę w godzinach nadliczbowych).</summary>
    OvertimeTimeOff = 11,

    /// <summary>Dzień wolny za święto przypadające w sobotę.</summary>
    HolidayTimeOff = 12,

    /// <summary>Zwolnienie od pracy z zachowaniem wynagrodzenia.</summary>
    PaidRelease = 13,
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

/// <summary>
/// A leave request with the employee's name resolved. <see cref="Days"/> are working days; <see cref="Hours"/> are the
/// working hours it takes; <see cref="IsHourly"/> marks leave taken in hours on one day.
/// </summary>
public sealed record LeaveRequestResponse(
    Guid Id,
    Guid EmployeeId,
    string EmployeeNumber,
    string EmployeeName,
    LeaveKind Kind,
    DateOnly StartDate,
    DateOnly EndDate,
    int Days,
    decimal Hours,
    bool IsHourly,
    string? Note,
    LeaveState State,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DecidedAt,
    string? DecisionNote);

/// <summary>A new request. <see cref="Hours"/> is set only for leave taken in hours, on one day (start equal to end).</summary>
public sealed record CreateLeaveRequest(Guid EmployeeId, LeaveKind Kind, DateOnly StartDate, DateOnly EndDate, string? Note, decimal? Hours = null);

/// <summary>Approve, reject or cancel. The note is required when rejecting.</summary>
public sealed record LeaveDecisionRequest(string? Note);

/// <summary>
/// Annual leave of one employee in one year, kept in hours as the Labour Code counts it (one day of entitlement is 8 hours).
/// Used and pending count annual and on-demand leave; remaining is what can still be requested. <see cref="DailyHours"/>
/// is the employee's working day (less than 8 for part-time work), which one day of leave takes.
/// </summary>
public sealed record LeaveBalanceResponse(
    Guid EmployeeId,
    int Year,
    bool HasEntitlement,
    int EntitlementDays,
    int CarriedOverDays,
    decimal TotalHours,
    decimal UsedHours,
    decimal PendingHours,
    decimal RemainingHours,
    int OnDemandDays,
    decimal DailyHours);

public sealed record SaveLeaveEntitlementRequest(int Days, int CarriedOverDays);

/// <summary>A public holiday in Poland: a day off for everyone, not counted as leave.</summary>
public sealed record HolidayResponse(DateOnly Date, string Name);
