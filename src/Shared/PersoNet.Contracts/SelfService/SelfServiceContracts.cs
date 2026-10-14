using PersoNet.Contracts.Leave;

namespace PersoNet.Contracts.SelfService;

/// <summary>
/// The signed-in person's own employee record, found by the e-mail of their account. <see cref="TeamSize"/> counts the
/// current employees who report to them; above zero they decide their team's leave requests in the portal.
/// </summary>
public sealed record MyProfileResponse(
    Guid EmployeeId,
    string Number,
    string FirstName,
    string LastName,
    string? Email,
    string? PositionName,
    string? DepartmentName,
    string? ManagerName,
    DateOnly HireDate,
    int TeamSize);

/// <summary>A leave request the signed-in employee files for themselves.</summary>
public sealed record MyLeaveRequest(LeaveKind Kind, DateOnly StartDate, DateOnly EndDate, string? Note, decimal? Hours = null);
