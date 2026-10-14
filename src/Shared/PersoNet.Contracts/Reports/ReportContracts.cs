using PersoNet.Contracts.Employees;

namespace PersoNet.Contracts.Reports;

/// <summary>
/// Who works in the company on a day: everyone employed then, with totals per department. <c>FullTimeEquivalent</c> is the
/// sum of the working time of employment contracts ("etaty"); civil contracts count as 0.
/// </summary>
public sealed record HeadcountReport(
    DateOnly Date,
    int People,
    decimal FullTimeEquivalent,
    IReadOnlyList<HeadcountDepartment> Departments,
    IReadOnlyList<HeadcountPerson> Rows);

/// <summary>People of one department on the day; <paramref name="DepartmentName"/> is null for people without one.</summary>
public sealed record HeadcountDepartment(string? DepartmentName, int People, decimal FullTimeEquivalent);

/// <summary>
/// One employed person and the contract they work under on the day: an employment contract when they have one, otherwise a
/// civil one; null when no contract covers the day.
/// </summary>
public sealed record HeadcountPerson(
    Guid EmployeeId,
    string Number,
    string FirstName,
    string LastName,
    string? DepartmentName,
    string? PositionName,
    DateOnly HireDate,
    ContractKind? ContractKind,
    DateOnly? ContractEndDate,
    decimal? WorkingTime);

/// <summary>Annual leave (with leave on demand) of everyone employed in the year: entitlement, taken, waiting and left.</summary>
public sealed record LeaveUsageReport(int Year, IReadOnlyList<LeaveUsageRow> Rows);

/// <summary>
/// The annual leave of one person. Amounts are in hours, like the balance; <paramref name="DailyHours"/> (8 hours times the
/// working time) turns them into days.
/// </summary>
public sealed record LeaveUsageRow(
    Guid EmployeeId,
    string Number,
    string FirstName,
    string LastName,
    string? DepartmentName,
    bool HasEntitlement,
    int EntitlementDays,
    int CarriedOverDays,
    decimal TotalHours,
    decimal UsedHours,
    decimal PendingHours,
    decimal RemainingHours,
    int OnDemandDays,
    decimal DailyHours);

/// <summary>Contracts with a planned end between <paramref name="From"/> and <paramref name="To"/>, soonest first.</summary>
public sealed record ContractsEndingReport(DateOnly From, DateOnly To, IReadOnlyList<ContractEndingRow> Rows);

/// <summary>A contract that ends in the period; <paramref name="HasNextContract"/> tells whether another one already follows it.</summary>
public sealed record ContractEndingRow(
    Guid ContractId,
    Guid EmployeeId,
    string Number,
    string FirstName,
    string LastName,
    string? DepartmentName,
    string? PositionName,
    ContractKind Kind,
    DateOnly StartDate,
    DateOnly EndDate,
    decimal? WorkingTime,
    bool HasNextContract);
