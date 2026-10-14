using System.Text.Json.Serialization;

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

/// <summary>Column the employee list is sorted by; sent as text, e.g. "HireDate". Ties are broken by name.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<EmployeeSort>))]
public enum EmployeeSort
{
    /// <summary>Last name, then first name.</summary>
    Name = 0,
    Number = 1,
    Position = 2,
    Department = 3,
    Manager = 4,
    HireDate = 5,

    /// <summary>Employed people first, then former ones by their last day.</summary>
    Status = 6,
}

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

/// <summary>Hires a former employee again from <see cref="HireDate"/>, after the last day of the previous employment.</summary>
public sealed record RehireEmployeeRequest(DateOnly HireDate);
