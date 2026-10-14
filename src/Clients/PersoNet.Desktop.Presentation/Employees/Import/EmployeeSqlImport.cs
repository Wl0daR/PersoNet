using System.Globalization;
using PersoNet.Contracts.Employees;

namespace PersoNet.Desktop.Presentation.Employees.Import;

/// <summary>
/// An employee read from the files, ready to send (<see cref="Row"/>) or with the reason it cannot be
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
    string? Problem,
    string? DepartmentCode = null,
    string? DepartmentName = null,
    string? Position = null,
    IReadOnlyList<ImportContractPeriod>? Contracts = null)
{
    public ImportEmployeeRow? Row => Problem is null && HireDate is { } hired
        ? new ImportEmployeeRow(Line, Number, FirstName, LastName, Phone, hired, TerminationDate, AnnualLeaveDays, DepartmentCode, Position, Contracts)
        : null;
}

/// <summary>An employer of the earlier system; a temporary work agency's people count towards the employer using it.</summary>
public sealed record LegacyEmployer(string Id, string Name);

/// <summary>What one company gets from the files: its departments, the positions its people hold, and its employees.</summary>
public sealed record EmployeeImportPlan(
    IReadOnlyList<ImportDepartment> Departments,
    IReadOnlyList<ImportPosition> Positions,
    IReadOnlyList<EmployeeFileEntry> Employees,
    int OtherEmployersEmployees);

/// <summary>
/// The earlier system's MySQL database ("Kadry"), read from its .sql dumps: <c>Employees</c> (required), and when given
/// <c>wydzialy</c> and <c>Departments</c> (the department tree), <c>Positions</c>, <c>EmploymentHistory</c> (periods of
/// employment, which become contracts, and the employer) and <c>Companies</c> (employers and temporary work agencies).
/// <c>EmployeeID</c> becomes the personnel number, <c>UrlopR</c> the yearly leave in days; birth date, gender and status
/// are not taken.
/// </summary>
public sealed class EmployeeSqlImport
{
    public const string Table = "Employees";

    public static readonly IReadOnlyList<string> Tables = [Table, "wydzialy", "Departments", "Positions", "EmploymentHistory", "Companies"];

    private readonly Dictionary<string, List<SqlRow>> _tables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _employerOfCompany = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _departmentOwner = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _divisionOwner = new(StringComparer.Ordinal);

    private EmployeeSqlImport()
    {
    }

    /// <summary>Employers found in the files; one "all" employer when the files have no <c>Companies</c>.</summary>
    public IReadOnlyList<LegacyEmployer> Employers { get; private set; } = [];

    public bool HasEmployees => Rows(Table).Count > 0;

    /// <summary>Which of the known tables were found, e.g. for "Wczytano: Employees, Departments".</summary>
    public IReadOnlyList<string> FoundTables => [.. Tables.Where(table => Rows(table).Count > 0)];

    /// <summary>Reads the given dumps (the text of each .sql file); tables may be spread over several files.</summary>
    public static EmployeeSqlImport Read(IEnumerable<string> dumps)
    {
        ArgumentNullException.ThrowIfNull(dumps);

        var import = new EmployeeSqlImport();
        var unreadable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dump in dumps)
        {
            foreach (var (table, rows) in SqlDumpReader.ReadTables(dump, Tables, unreadable))
            {
                if (!import._tables.TryGetValue(table, out var list))
                {
                    import._tables[table] = list = [];
                }

                list.AddRange(rows);
            }
        }

        // Without the employees there is nothing to import; another table without columns is simply left out.
        if (unreadable.Contains(Table))
        {
            throw new FormatException(SqlDumpReader.NoColumns(Table));
        }

        import.WorkOutEmployers();
        return import;
    }

    /// <summary>The employees of a single dump, all of them (the first version of the import).</summary>
    public static IReadOnlyList<EmployeeFileEntry> Read(string sql)
    {
        var import = Read([sql]);
        return import.Plan(import.Employers[0]).Employees;
    }

    public EmployeeImportPlan Plan(LegacyEmployer employer)
    {
        ArgumentNullException.ThrowIfNull(employer);

        var divisions = Rows("wydzialy").Where(row => Text(row, "id") is { } id && Owner(_divisionOwner, id) == employer.Id).ToList();
        var divisionIds = divisions.Select(row => Text(row, "id")!).ToHashSet(StringComparer.Ordinal);
        var departments = Rows("Departments").Where(row => Text(row, "DepartmentID") is { } id && Owner(_departmentOwner, id) == employer.Id).ToList();
        var departmentNames = departments.ToDictionary(row => Text(row, "DepartmentID")!, row => Text(row, "DepartmentName") ?? string.Empty, StringComparer.Ordinal);

        var tree = new List<ImportDepartment>();
        tree.AddRange(divisions.Select(row => new ImportDepartment("W" + Text(row, "id"), Text(row, "nazwa") ?? "Wydział " + Text(row, "id"), null)));
        tree.AddRange(departments.Select(row => new ImportDepartment(
            "D" + Text(row, "DepartmentID"),
            Text(row, "DepartmentName") ?? "Dział " + Text(row, "DepartmentID"),
            Text(row, "DepartmentType") is { } type && divisionIds.Contains(type) ? "W" + type : null)));

        var positionNames = Rows("Positions")
            .Where(row => Text(row, "PositionID") is not null && Text(row, "PositionName") is not null)
            .GroupBy(row => Text(row, "PositionID")!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => Text(group.First(), "PositionName")!, StringComparer.Ordinal);
        var history = Rows("EmploymentHistory")
            .Where(row => Text(row, "EmployeeID") is not null)
            .GroupBy(row => Text(row, "EmployeeID")!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(row => Text(row, "StartDate"), StringComparer.Ordinal).ToList(), StringComparer.Ordinal);

        var employees = new List<EmployeeFileEntry>();
        var others = 0;
        foreach (var row in Rows(Table))
        {
            var id = Text(row, "EmployeeID") ?? string.Empty;
            var periods = history.GetValueOrDefault(id) ?? [];
            if (EmployerOf(row, periods) != employer.Id)
            {
                others++;
                continue;
            }

            var latest = periods.Count > 0 ? periods[^1] : null;
            // The card's own department and position, or else the latest period's, when the card's is not in the files.
            var departmentId = new[] { Id(Text(row, "DepartmentID")), Id(latest is null ? null : Text(latest, "DepartmentID")) }
                .FirstOrDefault(candidate => candidate is not null && departmentNames.ContainsKey(candidate));
            var positionId = new[] { Id(Text(row, "PositionID")), Id(latest is null ? null : Text(latest, "PositionID")) }
                .FirstOrDefault(candidate => candidate is not null && positionNames.ContainsKey(candidate));
            var department = departmentId is not null && departmentNames.TryGetValue(departmentId, out var name) ? (Code: "D" + departmentId, Name: name) : default;
            var position = positionId is not null ? positionNames.GetValueOrDefault(positionId)?.Trim() : null;

            employees.Add(ToEntry(row) with
            {
                DepartmentCode = department.Code,
                DepartmentName = department.Name,
                Position = string.IsNullOrEmpty(position) ? null : position,
                Contracts = Contracts(periods.Where(period => PeriodEmployer(period) == employer.Id)),
            });
        }

        // Each position goes with the departments of this employer it was tied to in the earlier system (0 = none).
        var departmentsOfName = Rows("Positions")
            .Where(row => Text(row, "PositionName") is not null && Id(Text(row, "DepartmentID")) is { } id && departmentNames.ContainsKey(id))
            .GroupBy(row => Text(row, "PositionName")!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Select(row => "D" + Id(Text(row, "DepartmentID"))).Distinct(StringComparer.Ordinal).ToList(), StringComparer.OrdinalIgnoreCase);
        var positions = employees.Where(entry => entry.Position is not null).Select(entry => entry.Position!)
            .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.CurrentCulture)
            .Select(name => new ImportPosition(name, departmentsOfName.GetValueOrDefault(name) ?? []))
            .ToList();

        return new EmployeeImportPlan(tree, positions, employees, others);
    }

    // Employers are the companies that are not agencies; each agency belongs to the employer whose name it starts with,
    // or else to the first one. A division belongs to the employer whose name starts with the division's ("Prima" →
    // "Prima-KL"), as the earlier system's reports had it, or else to the first employer; a department follows its
    // division, and one outside any division goes to the employer most of its people worked for.
    private void WorkOutEmployers()
    {
        var companies = Rows("Companies").Where(row => Text(row, "CompanyID") is not null).ToList();
        var employers = companies.Where(row => Text(row, "IsTemporaryEmployee") is "0")
            .Select(row => new LegacyEmployer(Text(row, "CompanyID")!, Text(row, "CompanyName") ?? "Firma " + Text(row, "CompanyID")))
            .OrderBy(employer => int.TryParse(employer.Id, CultureInfo.InvariantCulture, out var id) ? id : int.MaxValue)
            .ToList();
        if (employers.Count == 0)
        {
            employers.Add(new LegacyEmployer(string.Empty, "Wszyscy pracownicy"));
        }

        Employers = employers;
        foreach (var company in companies)
        {
            var name = Text(company, "CompanyName") ?? string.Empty;
            var own = employers.FirstOrDefault(employer => employer.Id == Text(company, "CompanyID"))
                ?? employers.Where(employer => name.StartsWith(employer.Name, StringComparison.OrdinalIgnoreCase)).MaxBy(employer => employer.Name.Length)
                ?? employers[0];
            _employerOfCompany[Text(company, "CompanyID")!] = own.Id;
        }

        foreach (var division in Rows("wydzialy").Where(row => Text(row, "id") is not null))
        {
            var name = Text(division, "nazwa") ?? string.Empty;
            var own = name.Length >= 3
                ? employers.FirstOrDefault(employer => employer.Name.StartsWith(name, StringComparison.OrdinalIgnoreCase)) ?? employers[0]
                : employers[0];
            _divisionOwner[Text(division, "id")!] = own.Id;
        }

        var votes = Rows("EmploymentHistory")
            .Where(row => Text(row, "DepartmentID") is not null)
            .GroupBy(row => Text(row, "DepartmentID")!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => Majority(group.Select(row => Employer(Text(row, "CompanyID")))), StringComparer.Ordinal);
        foreach (var department in Rows("Departments").Where(row => Text(row, "DepartmentID") is not null))
        {
            var id = Text(department, "DepartmentID")!;
            _departmentOwner[id] = Text(department, "DepartmentType") is { } division && _divisionOwner.TryGetValue(division, out var owner)
                ? owner
                : votes.GetValueOrDefault(id) ?? employers[0].Id;
        }
    }

    // The employer is the one owning the department the person last worked in; without one, the employer on the period.
    private string EmployerOf(SqlRow employee, List<SqlRow> periods)
    {
        var latest = periods.Count > 0 ? periods[^1] : null;
        foreach (var department in new[] { Id(latest is null ? null : Text(latest, "DepartmentID")), Id(Text(employee, "DepartmentID")) })
        {
            if (department is not null && _departmentOwner.TryGetValue(department, out var owner))
            {
                return owner;
            }
        }

        return latest is null ? Employers[0].Id : Employer(Text(latest, "CompanyID"));
    }

    private string PeriodEmployer(SqlRow period) =>
        Id(Text(period, "DepartmentID")) is { } department && _departmentOwner.TryGetValue(department, out var owner) ? owner : Employer(Text(period, "CompanyID"));

    private string Employer(string? companyId) =>
        companyId is not null && _employerOfCompany.TryGetValue(companyId, out var employer) ? employer : Employers[0].Id;

    private string Owner(Dictionary<string, string> owners, string id) => owners.GetValueOrDefault(id) ?? Employers[0].Id;

    private static string Majority(IEnumerable<string> employers) =>
        employers.GroupBy(employer => employer, StringComparer.Ordinal).OrderByDescending(group => group.Count()).First().Key;

    private List<SqlRow> Rows(string table) => _tables.GetValueOrDefault(table) ?? [];

    // Periods that touch or overlap are one employment (a change of position, for example); 8 hours a day is full time.
    private static List<ImportContractPeriod> Contracts(IEnumerable<SqlRow> periods)
    {
        var contracts = new List<ImportContractPeriod>();
        foreach (var period in periods)
        {
            if (Date(period, "StartDate", out _) is not { } start)
            {
                continue;
            }

            var end = Date(period, "EndDate", out _);
            var hours = int.TryParse(Text(period, "Workload"), CultureInfo.InvariantCulture, out var workload) && workload > 0 ? workload : 8;
            var workingTime = Math.Min(1m, Math.Round(hours / 8m, 4));

            if (contracts.Count > 0 && contracts[^1] is var last && (last.EndDate is null || start <= last.EndDate.Value.AddDays(1)))
            {
                contracts[^1] = last with { EndDate = last.EndDate is null || end is null ? null : (end > last.EndDate ? end : last.EndDate) };
                continue;
            }

            contracts.Add(new ImportContractPeriod(start, end, workingTime));
        }

        return contracts;
    }

    private static string? Id(string? id) => id is null or "0" ? null : id;

    private static EmployeeFileEntry ToEntry(SqlRow row)
    {
        var problems = new List<string>();

        var number = Text(row, "EmployeeID") ?? string.Empty;
        var first = Text(row, "FirstName") ?? string.Empty;
        var last = Text(row, "LastName") ?? string.Empty;
        if (number.Length == 0)
        {
            problems.Add("brak EmployeeID");
        }

        if (first.Length == 0 || last.Length == 0)
        {
            problems.Add("brak imienia lub nazwiska");
        }

        var hired = Date(row, "HireDate", out var badHire);
        if (hired is null)
        {
            problems.Add(badHire ? "nieprawidłowa data zatrudnienia" : "brak daty zatrudnienia");
        }

        var terminated = Date(row, "TerminationDate", out var badTermination);
        if (badTermination)
        {
            problems.Add("nieprawidłowa data zwolnienia");
        }
        else if (hired is { } from && terminated is { } to && to < from)
        {
            problems.Add("data zwolnienia przed datą zatrudnienia");
        }

        int? days = null;
        if (Text(row, "UrlopR") is { } leave)
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

        return new EmployeeFileEntry(row.Line, number, first, last, Text(row, "PhoneNumber"), hired, terminated, days, problem);
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
