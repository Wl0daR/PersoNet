namespace PersoNet.Contracts.Leave;

/// <summary>
/// Who is on leave in a month: everyone employed in that month, with their approved leave and the requests still waiting
/// for a decision, plus the month's public holidays.
/// </summary>
public sealed record LeaveCalendarResponse(int Year, int Month, IReadOnlyList<HolidayResponse> Holidays, IReadOnlyList<LeaveCalendarPerson> People);

/// <summary>One row of the calendar: a person and their leave that touches the month.</summary>
public sealed record LeaveCalendarPerson(
    Guid EmployeeId,
    string FirstName,
    string LastName,
    string? DepartmentName,
    string? PositionName,
    IReadOnlyList<LeaveCalendarEntry> Leave);

/// <summary>A leave of the person; it may start before or end after the month.</summary>
public sealed record LeaveCalendarEntry(Guid RequestId, LeaveKind Kind, DateOnly StartDate, DateOnly EndDate, LeaveState State, bool IsHourly, decimal Hours);
