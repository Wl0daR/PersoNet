namespace PersoNet.Contracts.Employees;

/// <summary>
/// One employee read from a file of an earlier system. <paramref name="Line"/> is where the row is in the file, so
/// problems can be pointed at; <paramref name="AnnualLeaveDays"/> becomes the leave entitlement of the current year.
/// <paramref name="DepartmentCode"/> and <paramref name="Position"/> name a department and a position imported before
/// (by code and by name); <paramref name="Contracts"/> are the periods of employment.
/// </summary>
public sealed record ImportEmployeeRow(
    int Line,
    string Number,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    int? AnnualLeaveDays,
    string? DepartmentCode = null,
    string? Position = null,
    IReadOnlyList<ImportContractPeriod>? Contracts = null);

/// <summary>
/// A period of employment from the earlier system, imported as a contract of employment for an indefinite period that
/// ended on <paramref name="EndDate"/> (null while it lasts). <paramref name="WorkingTime"/> is the fraction of full time.
/// </summary>
public sealed record ImportContractPeriod(DateOnly StartDate, DateOnly? EndDate, decimal WorkingTime);

public sealed record ImportEmployeesRequest(IReadOnlyList<ImportEmployeeRow> Rows);

/// <summary>A row that was not imported, and why.</summary>
public sealed record ImportRowProblem(int Line, string Number, string Message);

/// <summary>
/// What an import did. An employee whose personnel number is already in PersoNet keeps their data; only the department
/// and position are brought up to date, and contracts are added when they have none (<paramref name="Updated"/>, or
/// <paramref name="Existing"/> when nothing changed). So running the same files again adds nothing twice.
/// </summary>
public sealed record ImportEmployeesResponse(
    int Received,
    int Added,
    int Existing,
    int EntitlementsAdded,
    IReadOnlyList<ImportRowProblem> Problems,
    int Updated = 0,
    int ContractsAdded = 0);

/// <summary>
/// A department from the earlier system. <paramref name="Code"/> identifies it (for example "D12"), so a second import
/// finds it again; <paramref name="ParentCode"/> places it under another imported department.
/// </summary>
public sealed record ImportDepartment(string Code, string Name, string? ParentCode);

/// <summary>Departments and the dictionary of positions, imported before the employees that refer to them.</summary>
public sealed record ImportOrganizationRequest(IReadOnlyList<ImportDepartment> Departments, IReadOnlyList<string> Positions);

public sealed record ImportOrganizationResponse(
    int DepartmentsAdded,
    int DepartmentsExisting,
    int PositionsAdded,
    int PositionsExisting,
    IReadOnlyList<string> Problems);
