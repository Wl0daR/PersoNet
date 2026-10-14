using System.Globalization;
using PersoNet.Contracts.Employees;

namespace PersoNet.Desktop.Presentation.Employees.Import;

/// <summary>
/// An employee read from the file, ready to send (<see cref="Row"/>) or with the reason it cannot be
/// (<see cref="Problem"/>).
/// </summary>
public sealed record EmployeeFileEntry(
    int Line,
    string Number,
    string FirstName,
    string LastName,
    string? Phone,
    DateOnly? HireDate,
    DateOnly? TerminationDate,
    int? AnnualLeaveDays,
    string? Problem)
{
    public ImportEmployeeRow? Row => Problem is null && HireDate is { } hired
        ? new ImportEmployeeRow(Line, Number, FirstName, LastName, Phone, hired, TerminationDate, AnnualLeaveDays)
        : null;
}

/// <summary>
/// Employees from the earlier system's MySQL table <c>Employees</c>: <c>EmployeeID</c> becomes the personnel number,
/// <c>UrlopR</c> the yearly leave in days. Birth date, gender, status, position and department are not taken.
/// </summary>
public static class EmployeeSqlImport
{
    public const string Table = "Employees";

    private const string Id = "EmployeeID";
    private const string FirstName = "FirstName";
    private const string LastName = "LastName";
    private const string Phone = "PhoneNumber";
    private const string HireDate = "HireDate";
    private const string TerminationDate = "TerminationDate";
    private const string LeaveDays = "UrlopR";

    public static IReadOnlyList<EmployeeFileEntry> Read(string sql) =>
        [.. SqlDumpReader.ReadTable(sql, Table).Select(ToEntry)];

    private static EmployeeFileEntry ToEntry(SqlRow row)
    {
        var problems = new List<string>();

        var number = Text(row, Id) ?? string.Empty;
        var first = Text(row, FirstName) ?? string.Empty;
        var last = Text(row, LastName) ?? string.Empty;
        if (number.Length == 0)
        {
            problems.Add("brak EmployeeID");
        }

        if (first.Length == 0 || last.Length == 0)
        {
            problems.Add("brak imienia lub nazwiska");
        }

        var hired = Date(row, HireDate, out var badHire);
        if (hired is null)
        {
            problems.Add(badHire ? "nieprawidłowa data zatrudnienia" : "brak daty zatrudnienia");
        }

        var terminated = Date(row, TerminationDate, out var badTermination);
        if (badTermination)
        {
            problems.Add("nieprawidłowa data zwolnienia");
        }
        else if (hired is { } from && terminated is { } to && to < from)
        {
            problems.Add("data zwolnienia przed datą zatrudnienia");
        }

        int? days = null;
        if (Text(row, LeaveDays) is { } leave)
        {
            if (int.TryParse(leave, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed >= 0)
            {
                days = parsed;
            }
            else
            {
                problems.Add("nieprawidłowy wymiar urlopu");
            }
        }

        var problem = problems.Count == 0 ? null : string.Join(", ", problems);
        problem = problem is null ? null : char.ToUpper(problem[0], CultureInfo.CurrentCulture) + problem[1..] + ".";

        return new EmployeeFileEntry(row.Line, number, first, last, Text(row, Phone), hired, terminated, days, problem);
    }

    private static string? Text(SqlRow row, string column) =>
        row.Values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    // MySQL writes an unknown date as 0000-00-00, and some programs as 1899-12-30 (day zero of Excel and Delphi).
    private static DateOnly? Date(SqlRow row, string column, out bool invalid)
    {
        invalid = false;
        if (Text(row, column) is not { } text || text.StartsWith("0000-00-00", StringComparison.Ordinal))
        {
            return null;
        }

        var datePart = text.Length >= 10 ? text[..10] : text;
        if (!DateOnly.TryParseExact(datePart, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            invalid = true;
            return null;
        }

        return date.Year < 1901 ? null : date;
    }
}
