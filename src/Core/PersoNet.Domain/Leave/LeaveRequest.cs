using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Leave;

/// <summary>Kinds of leave HR records. Sick leave (L4) is not a request and is not modelled here.</summary>
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

    /// <summary>Zwolnienie na opiekę nad dzieckiem (art. 188 KP), at most 2 days a year.</summary>
    ChildCare = 5,
}

public enum LeaveStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Cancelled = 4,
}

/// <summary>
/// A request for days off: who, what kind, which days, and the decision on it. Days are counted as working days
/// (<see cref="PolishCalendar"/>) when the request is made. Limits that need the employee's other requests (balance, overlaps)
/// are checked by the caller.
/// </summary>
public sealed class LeaveRequest : Entity, ITenantOwned
{
    public const int NoteMaxLength = 500;

    // Used by EF Core.
    private LeaveRequest()
    {
    }

    private LeaveRequest(Guid tenantId, Guid employeeId, LeaveType type, DateOnly startDate, DateOnly endDate, int days, string? note, DateTimeOffset createdAt)
    {
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

    /// <summary>Working days the leave takes.</summary>
    public int Days { get; private set; }

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

    public static LeaveRequest Create(Guid tenantId, Guid employeeId, LeaveType type, DateOnly startDate, DateOnly endDate, string? note, DateTimeOffset createdAt)
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

        return new LeaveRequest(tenantId, employeeId, type, startDate, endDate, days, Clean(note), createdAt);
    }

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
