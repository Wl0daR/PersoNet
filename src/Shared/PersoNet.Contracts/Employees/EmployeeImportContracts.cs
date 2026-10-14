namespace PersoNet.Contracts.Employees;

/// <summary>
/// One employee read from a file of an earlier system. <paramref name="Line"/> is where the row is in the file, so
/// problems can be pointed at; <paramref name="AnnualLeaveDays"/> becomes the leave entitlement of the current year.
/// </summary>
public sealed record ImportEmployeeRow(
    int Line,
    string Number,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly HireDate,
    DateOnly? TerminationDate,
    int? AnnualLeaveDays);

public sealed record ImportEmployeesRequest(IReadOnlyList<ImportEmployeeRow> Rows);

/// <summary>A row that was not imported, and why.</summary>
public sealed record ImportRowProblem(int Line, string Number, string Message);

/// <summary>
/// What an import did. Employees whose personnel number is already in PersoNet are left as they are
/// (<paramref name="Existing"/>), so running the same file again adds nothing twice.
/// </summary>
public sealed record ImportEmployeesResponse(
    int Received,
    int Added,
    int Existing,
    int EntitlementsAdded,
    IReadOnlyList<ImportRowProblem> Problems);
