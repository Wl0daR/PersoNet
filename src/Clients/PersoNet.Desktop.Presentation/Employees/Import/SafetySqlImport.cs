using System.Globalization;
using PersoNet.Contracts.Absences;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Safety;

namespace PersoNet.Desktop.Presentation.Employees.Import;

/// <summary>
/// The trainings of one company's list (<see cref="Courses"/>), the trainings its employees completed and their medical
/// examinations, and the rows that cannot be imported (<see cref="Problems"/>); rows of other companies' people are only counted.
/// </summary>
public sealed record SafetyPlan(
    IReadOnlyList<ImportTrainingCourseRow> Courses,
    IReadOnlyList<ImportTrainingRow> Trainings,
    IReadOnlyList<ImportMedicalExamRow> Exams,
    IReadOnlyList<ImportLeaveProblem> Problems,
    int OtherEmployersRows)
{
    public static SafetyPlan None { get; } = new([], [], [], [], 0);

    public bool IsEmpty => Courses.Count + Trainings.Count + Exams.Count == 0;
}

/// <summary>
/// Occupational health and safety records of the earlier system: <c>szkolenia_definicje</c> (the list of trainings, with
/// <c>waznosc</c>, the validity in days, used when <c>czy_wygasa</c> is set), <c>szkolenia_rel</c> (the divisions of
/// <c>wydzialy</c> and the groups of <c>grupy</c> a training concerns), <c>szkolenia_prac</c> (trainings completed) and
/// <c>badania</c> (medical examinations). Dates are days since 30 December 1899. The older copy <c>szkolenia</c> is not
/// read: <c>szkolenia_prac</c> holds all of its rows.
/// </summary>
public sealed class SafetySqlImport
{
    public const string CoursesTable = "szkolenia_definicje";

    public const string RelationsTable = "szkolenia_rel";

    public const string TrainingsTable = "szkolenia_prac";

    public const string ExamsTable = "badania";

    public const string GroupsTable = "grupy";

    public static readonly IReadOnlyList<string> Tables = [CoursesTable, RelationsTable, TrainingsTable, ExamsTable, GroupsTable];

    private const double DaysPerMonth = 365.25 / 12;

    private static readonly DateOnly DayZero = new(1899, 12, 30);

    private readonly Dictionary<string, List<SqlRow>> _tables = new(StringComparer.OrdinalIgnoreCase);

    private SafetySqlImport()
    {
    }

    /// <summary>Which of the tables were found, e.g. for "Wczytane tabele: …, badania".</summary>
    public IReadOnlyList<string> FoundTables => [.. Tables.Where(table => Rows(table).Count > 0)];

    public bool HasRecords => Rows(CoursesTable).Count + Rows(ExamsTable).Count > 0;

    /// <summary>Reads the given dumps (the text of each .sql file); a table without columns is left out.</summary>
    public static SafetySqlImport Read(IEnumerable<string> dumps)
    {
        ArgumentNullException.ThrowIfNull(dumps);

        var import = new SafetySqlImport();
        foreach (var dump in dumps)
        {
            foreach (var (table, rows) in SqlDumpReader.ReadTables(dump, Tables))
            {
                if (!import._tables.TryGetValue(table, out var list))
                {
                    import._tables[table] = list = [];
                }

                list.AddRange(rows);
            }
        }

        return import;
    }

    /// <summary>
    /// What the company with these employees and departments gets: the trainings its people completed or that concern its
    /// departments, and the records of its people.
    /// </summary>
    public SafetyPlan Plan(EmployeeImportPlan company)
    {
        ArgumentNullException.ThrowIfNull(company);

        var numbers = company.Employees.Select(entry => entry.Number.Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var problems = new List<ImportLeaveProblem>();
        var others = 0;

        var names = Rows(CoursesTable).Where(row => Text(row, "id") is not null && Text(row, "nazwa") is not null)
            .ToDictionary(row => Text(row, "id")!, row => Text(row, "nazwa")!, StringComparer.Ordinal);

        var trainings = new List<ImportTrainingRow>();
        var expiring = Rows(CoursesTable).Where(row => Validity(row) is not null).Select(row => Text(row, "id")).ToHashSet(StringComparer.Ordinal);
        foreach (var row in Rows(TrainingsTable))
        {
            var number = Text(row, "idprac") ?? string.Empty;
            if (!numbers.Contains(number))
            {
                others++;
                continue;
            }

            if (Text(row, "idszkolenia") is not { } courseId || !names.TryGetValue(courseId, out var course))
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, "Szkolenia nie ma na liście szkoleń (szkolenia_definicje)."));
                continue;
            }

            if (Date(row, "data") is not { } completed)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, $"Szkolenie „{course}” bez daty."));
                continue;
            }

            // Only a training that expires keeps the end date the earlier system wrote; one that does not stays valid for good.
            var until = expiring.Contains(courseId) ? Date(row, "wygasa") : null;
            trainings.Add(new ImportTrainingRow(row.Line, number, course, completed, until >= completed ? until : null, Note(row)));
        }

        var exams = new List<ImportMedicalExamRow>();
        foreach (var row in Rows(ExamsTable))
        {
            var number = Text(row, "idprac") ?? string.Empty;
            if (!numbers.Contains(number))
            {
                others++;
                continue;
            }

            if (Date(row, "data_badania") is not { } examined)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, "Badanie lekarskie bez daty."));
                continue;
            }

            var until = Date(row, "wygasa");
            var note = Note(row);
            exams.Add(new ImportMedicalExamRow(row.Line, number, Kind(note), examined, until >= examined ? until : null, note));
        }

        var departments = Departments(company.Departments);
        var used = trainings.Select(training => training.CourseName).ToHashSet(StringComparer.Ordinal);
        var courses = Rows(CoursesTable)
            .Where(row => Text(row, "id") is not null && Text(row, "nazwa") is not null)
            .Select(row => Course(row, departments.GetValueOrDefault(Text(row, "id")!) ?? []))
            .Where(course => used.Contains(course.Name) || course.DepartmentCodes is { Count: > 0 })
            .ToList();

        return new SafetyPlan(courses, trainings, exams, problems, others);
    }

    private static ImportTrainingCourseRow Course(SqlRow row, IReadOnlyList<string> departments) => new(
        row.Line,
        Text(row, "nazwa")!,
        Validity(row),
        Text(row, "czy_obowiazkowe") == "1",
        departments,
        Text(row, "opis"));

    // Days of validity in whole months: 365 → 12, 1825 → 60. Under a month is no validity the earlier system could mean.
    private static int? Validity(SqlRow row)
    {
        if (Text(row, "czy_wygasa") != "1" || !int.TryParse(Text(row, "waznosc"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var days))
        {
            return null;
        }

        var months = (int)Math.Round(days / DaysPerMonth);
        return months is >= 1 and <= 240 ? months : null;
    }

    // The departments of this company each training concerns. A training given for groups (the departments of the
    // structure, matched by name) concerns those; one given only for divisions concerns the whole division (code W<id>).
    private Dictionary<string, IReadOnlyList<string>> Departments(IReadOnlyList<ImportDepartment> structure)
    {
        var known = structure.Select(department => department.Code).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var byName = structure.Where(department => department.Code.StartsWith('D'))
            .GroupBy(department => department.Name.Trim(), StringComparer.CurrentCultureIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Code, StringComparer.CurrentCultureIgnoreCase);
        var groups = Rows(GroupsTable).Where(row => Text(row, "id") is not null && Text(row, "nazwa") is not null)
            .ToDictionary(row => Text(row, "id")!, row => byName.GetValueOrDefault(Text(row, "nazwa")!), StringComparer.Ordinal);

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (var course in Rows(RelationsTable).Where(row => Text(row, "id_szkolenia") is not null).GroupBy(row => Text(row, "id_szkolenia")!, StringComparer.Ordinal))
        {
            var ofGroups = course.Select(row => Text(row, "id_grupy") is { } id and not "0" ? groups.GetValueOrDefault(id) : null).OfType<string>().ToList();
            var ofDivisions = course.Select(row => Text(row, "id_wydzialu") is { } id and not "0" ? "W" + id : null).OfType<string>().ToList();
            result[course.Key] = (ofGroups.Count > 0 ? ofGroups : ofDivisions).Where(known.Contains).Distinct(StringComparer.Ordinal).ToList();
        }

        return result;
    }

    // The earlier system did not record the kind; its note sometimes says it.
    private static MedicalExamKind Kind(string? note) => note?.ToUpperInvariant() switch
    {
        { } text when text.Contains("WSTĘPN", StringComparison.Ordinal) => MedicalExamKind.Initial,
        { } text when text.Contains("KONTROL", StringComparison.Ordinal) => MedicalExamKind.Control,
        _ => MedicalExamKind.Periodic,
    };

    // "Memo1" is the empty note of the earlier program's form, not something HR wrote.
    private static string? Note(SqlRow row) => Text(row, "uwagi") is { } note && !note.Equals("Memo1", StringComparison.OrdinalIgnoreCase)
        ? note[..Math.Min(note.Length, 500)]
        : null;

    private static DateOnly? Date(SqlRow row, string column) =>
        int.TryParse(Text(row, column), NumberStyles.Integer, CultureInfo.InvariantCulture, out var day) && day is > 365 and < 80_000
            ? DayZero.AddDays(day)
            : null;

    private List<SqlRow> Rows(string table) => _tables.TryGetValue(table, out var rows) ? rows : [];

    private static string? Text(SqlRow row, string column) =>
        row.Values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;
}
