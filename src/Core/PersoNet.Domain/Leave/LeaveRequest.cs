using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Leave;

/// <summary>Kinds of leave HR records. Sick leave (L4) is not a request; it is an <c>Absence</c>.</summary>
public enum LeaveType
{
    /// <summary>Urlop wypoczynkowy, taken from the yearly entitlement.</summary>
    Annual = 1,

    /// <summary>Urlop na żądanie (art. 167² KP): part of the annual leave, at most 4 days a year.</summary>
    OnDemand = 2,

    /// <summary>Urlop okolicznościowy (wedding, birth, funeral).</summary>
    Occasional = 3,

    /// <summary>Urlop bezpłatny.</summary>
    Unpaid = 4,

    /// <summary>Zwolnienie na opiekę nad dzieckiem (art. 188 KP): 2 days or 16 hours a year.</summary>
    ChildCare = 5,

    /// <summary>Zwolnienie z powodu siły wyższej (art. 148¹ KP): 2 days or 16 hours a year, paid at half rate.</summary>
    ForceMajeure = 6,

    /// <summary>Urlop macierzyński (art. 180 KP).</summary>
    Maternity = 7,

    /// <summary>Urlop ojcowski (art. 182³ KP).</summary>
    Paternity = 8,

    /// <summary>Urlop rodzicielski (art. 182¹ᵃ KP).</summary>
    Parental = 9,

    /// <summary>Urlop wychowawczy (art. 186 KP).</summary>
    ChildRearing = 10,

    /// <summary>Czas wolny za pracę w godzinach nadliczbowych (art. 151² KP).</summary>
    OvertimeTimeOff = 11,

    /// <summary>Dzień wolny za święto przypadające w sobotę (art. 130 § 2 KP).</summary>
    HolidayTimeOff = 12,

    /// <summary>Zwolnienie od pracy z zachowaniem prawa do wynagrodzenia (other paid time off agreed with HR).</summary>
    PaidRelease = 13,
}

public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

/// <summary>
/// A request for time off: who, what kind, which days, and the decision on it. Days are counted as working days
/// (<see cref="PolishCalendar"/>) when the request is made, and <see cref="Hours"/> is what the request takes from the
/// employee's time: the employee's daily working hours for each day, or the hours of a single day when taken in hours.
/// Limits that need the employee's other requests (balance, overlaps, the hourly-leave rules) are checked by the caller.
/// </summary>
public sealed class LeaveRequest : Entity, ITenantOwned
{
    public const int NoteMaxLength = 500;

    // Used by EF Core.
    private LeaveRequest()
    {
    }

    private LeaveRequest(Guid tenantId, Guid employeeId, LeaveType type, DateOnly startDate, DateOnly endDate, int days, decimal hours, bool isHourly, string? note, DateTimeOffset createdAt)
    {
        Hours = hours;
        IsHourly = isHourly;
        TenantId = tenantId;
        EmployeeId = employeeId;
        Type = type;
        StartDate = startDate;
        EndDate = endDate;
        Days = days;
        Note = note;
        CreatedAt = createdAt;
        Status = LeaveStatus.Pending;
    }

    public Guid TenantId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public LeaveType Type { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>Last day of leave (inclusive).</summary>
    public DateOnly EndDate { get; private set; }

    /// <summary>Working days the leave takes (1 for leave taken in hours).</summary>
    public int Days { get; private set; }

    /// <summary>Working hours the leave takes.</summary>
    public decimal Hours { get; private set; }

    /// <summary>True when taken in hours on a single day rather than in whole days.</summary>
    public bool IsHourly { get; private set; }

    public string? Note { get; private set; }

    public LeaveStatus Status { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>User who approved, rejected or cancelled the request.</summary>
    public Guid? DecidedBy { get; private set; }

    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Reason given with the decision; required when rejecting.</summary>
    public string? DecisionNote { get; private set; }

    /// <summary>True while the request still holds its days (waiting or approved).</summary>
    public bool IsActive => Status is LeaveStatus.Pending or LeaveStatus.Approved;

    /// <summary>True for kinds taken from the yearly annual leave entitlement.</summary>
    public bool UsesAnnualLeave => UsesAnnualLeaveType(Type);

    public static bool UsesAnnualLeaveType(LeaveType type) => type is LeaveType.Annual or LeaveType.OnDemand;

    /// <summary>
    /// Kinds the Labour Code lets take in hours: child care and force majeure (art. 188, 148¹), and annual leave when what is
    /// left of it is less than a full day (art. 154² § 2; checked by the caller).
    /// </summary>
    public static bool AllowsHoursType(LeaveType type) => type is LeaveType.Annual or LeaveType.ChildCare or LeaveType.ForceMajeure;

    /// <summary>
    /// Creates a request. <paramref name="dailyHours"/> is the employee's working time on a day (8 for full time);
    /// <paramref name="hours"/> is set only for leave taken in hours, which is one working day and at most a full day.
    /// </summary>
    public static LeaveRequest Create(
        Guid tenantId,
        Guid employeeId,
        LeaveType type,
        DateOnly startDate,
        DateOnly endDate,
        decimal dailyHours,
        decimal? hours,
        string? note,
        DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty || employeeId == Guid.Empty)
        {
            throw new DomainException("Leave must belong to a tenant and an employee.");
        }

        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Unknown leave type.");
        }

        if (endDate < startDate)
        {
            throw new DomainException("Leave cannot end before it starts.");
        }

        if (startDate.Year != endDate.Year)
        {
            throw new DomainException("Leave must fit in one calendar year.");
        }

        var days = PolishCalendar.WorkingDays(startDate, endDate);
        if (days == 0)
        {
            throw new DomainException("Leave must include at least one working day.");
        }

        if (dailyHours is <= 0 or > 24)
        {
            throw new DomainException("Daily working hours must be between 0 and 24.");
        }

        if (hours is not { } partOfDay)
        {
            return new LeaveRequest(tenantId, employeeId, type, startDate, endDate, days, days * dailyHours, isHourly: false, Clean(note), createdAt);
        }

        if (!AllowsHoursType(type))
        {
            throw new DomainException("This kind of leave cannot be taken in hours.");
        }

        if (startDate != endDate)
        {
            throw new DomainException("Leave in hours is taken on one day.");
        }

        if (!IsValidHours(partOfDay, dailyHours))
        {
            throw new DomainException("Hours must be more than 0, at most the working day, in quarters of an hour.");
        }

        return new LeaveRequest(tenantId, employeeId, type, startDate, endDate, days, partOfDay, isHourly: true, Clean(note), createdAt);
    }

    /// <summary>More than zero, at most a working day, and in whole quarters of an hour (15 minutes).</summary>
    public static bool IsValidHours(decimal hours, decimal dailyHours) => hours > 0 && hours <= dailyHours && hours * 4 == decimal.Truncate(hours * 4);

    public void Approve(Guid decidedBy, DateTimeOffset at, string? note)
    {
        EnsurePending();
        Decide(LeaveStatus.Approved, decidedBy, at, note);
    }

    public void Reject(Guid decidedBy, DateTimeOffset at, string reason)
    {
        EnsurePending();

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new DomainException("A rejection needs a reason.");
        }

        Decide(LeaveStatus.Rejected, decidedBy, at, reason);
    }

    /// <summary>Withdraws a waiting or approved request; its days return to the balance.</summary>
    public void Cancel(Guid decidedBy, DateTimeOffset at, string? note)
    {
        if (!IsActive)
        {
            throw new DomainException("Only a waiting or approved request can be cancelled.");
        }

        Decide(LeaveStatus.Cancelled, decidedBy, at, note);
    }

    /// <summary>True when both periods share at least one day.</summary>
    public bool Overlaps(DateOnly start, DateOnly end) => StartDate <= end && start <= EndDate;

    private void EnsurePending()
    {
        if (Status != LeaveStatus.Pending)
        {
            throw new DomainException("The request has already been decided.");
        }
    }

    private void Decide(LeaveStatus status, Guid decidedBy, DateTimeOffset at, string? note)
    {
        Status = status;
        DecidedBy = decidedBy;
        DecidedAt = at;
        DecisionNote = Clean(note);
    }

    private static string? Clean(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        return trimmed.Length <= NoteMaxLength ? trimmed : throw new DomainException($"A note can have at most {NoteMaxLength} characters.");
    }
}
