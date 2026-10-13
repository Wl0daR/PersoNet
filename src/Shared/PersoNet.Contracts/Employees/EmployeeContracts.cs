namespace PersoNet.Contracts.Employees;

/// <summary>An employee's card as HR sees it.</summary>
public sealed record EmployeeResponse(
    Guid Id,
    string Number,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    Guid? DepartmentId,
    Guid? PositionId,
    Guid? ManagerId);

/// <summary>One row of the employee list, with names of the department, position and manager resolved.</summary>
public sealed record EmployeeListItem(
    Guid Id,
    string Number,
    string FirstName,
    string LastName,
    string? Email,
    string? DepartmentName,
    string? PositionName,
    string? ManagerName,
    DateOnly HireDate,
    DateOnly? TerminationDate);

/// <summary>Minimal employee entry for pickers (for example choosing a manager).</summary>
public sealed record EmployeeLookupItem(Guid Id, string Number, string FullName);

/// <summary>Create and update take the same fields.</summary>
public sealed record SaveEmployeeRequest(
    string Number,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateOnly HireDate,
    Guid? DepartmentId,
    Guid? PositionId,
    Guid? ManagerId);

public sealed record TerminateEmployeeRequest(DateOnly LastDay);
