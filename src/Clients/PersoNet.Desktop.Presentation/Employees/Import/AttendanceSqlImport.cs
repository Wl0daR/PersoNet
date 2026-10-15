using System.Globalization;
using System.Text.RegularExpressions;
using PersoNet.Contracts.Absences;
using PersoNet.Contracts.WorkTime;

namespace PersoNet.Desktop.Presentation.Employees.Import;

/// <summary>
/// Days of the employees of one company from <c>obecnosc</c>: days worked (<see cref="Days"/>), and the absences and paid
/// releases written into the day (<see cref="Absences"/>, <see cref="Leave"/>), each run of days of one kind as one
/// period. <see cref="Empty"/> counts days with neither hours nor a kind; rows of other companies' people are only counted.
/// </summary>
public sealed record AttendancePlan(
    IReadOnlyList<ImportWorkDayRow> Days,
    IReadOnlyList<ImportLeaveRow> Leave,
    IReadOnlyList<ImportAbsenceRow> Absences,
    IReadOnlyList<ImportLeaveProblem> Problems,
    int Empty,
    int OtherEmployersRows)
{
    public static AttendancePlan None { get; } = new([], [], [], [], 0, 0);

    public bool IsEmpty => Days.Count + Leave.Count + Absences.Count == 0;
}

/// <summary>
/// The earlier system's attendance table <c>obecnosc</c>, read row by row from its .sql dump (about a million rows).
/// <c>dzien</c> is the day and <c>od</c>/<c>do</c> the start and end, as days since 30 December 1899 with the time as a
/// fraction; <c>inne</c> holds the short name of an absence instead (NN, DEL, ZP, …). The raw clock readings
/// (<c>wejscie</c>, <c>wyjscie</c>) are not taken: the hours HR accepted are.
/// </summary>
public sealed partial class AttendanceSqlImport
{
    public const string Table = "obecnosc";

    private static readonly DateOnly DayZero = new(1899, 12, 30);

    private readonly List<Entry> _entries = [];

    // A million rows hold under two thousand personnel numbers; one string each keeps the memory down.
    private readonly Dictionary<string, string> _numbers = new(StringComparer.Ordinal);

    private AttendanceSqlImport()
    {
    }

    /// <summary>Rows read, with or without anything to import.</summary>
    public int Count => _entries.Count;

    public static AttendanceSqlImport Read(TextReader reader)
    {
        var import = new AttendanceSqlImport();
        foreach (var row in SqlDumpReader.StreamTable(reader, Table))
        {
            var entry = ToEntry(row);
            import._entries.Add(entry with { Number = import.Intern(entry.Number) });
        }

        return import;
    }

    /// <summary>What the people with these personnel numbers get.</summary>
    public AttendancePlan Plan(IReadOnlySet<string> numbers)
    {
        ArgumentNullException.ThrowIfNull(numbers);

        var days = new List<ImportWorkDayRow>();
        var coded = new List<Entry>();
        var problems = new List<ImportLeaveProblem>();
        int empty = 0, others = 0;

        foreach (var entry in _entries)
        {
            if (!numbers.Contains(entry.Number))
            {
                others++;
            }
            else if (entry.Date is null)
            {
                problems.Add(new ImportLeaveProblem(entry.Line, entry.Number, "Nieprawidłowa data dnia."));
            }
            else if (entry.Code is { } code)
            {
                if (ShortName().IsMatch(code))
                {
                    coded.Add(entry);
                }
                else
                {
                    problems.Add(new ImportLeaveProblem(entry.Line, entry.Number, $"Nieczytelny wpis „{code}” w dniu {entry.Date:dd.MM.yyyy}."));
                }
            }
            else if (entry.Start is { } start && entry.End is { } end && start != end)
            {
                days.Add(new ImportWorkDayRow(entry.Line, entry.Number, entry.Date.Value, start, end));
            }
            else
            {
                empty++;
            }
        }

        var (leave, absences) = Periods(coded);
        return new AttendancePlan(days, leave, absences, problems, empty, others);
    }

    // Days in a row of the same kind become one period: a business trip from Monday to Wednesday is one absence.
    private static (List<ImportLeaveRow> Leave, List<ImportAbsenceRow> Absences) Periods(List<Entry> coded)
    {
        var leave = new List<ImportLeaveRow>();
        var absences = new List<ImportAbsenceRow>();
        foreach (var run in coded.GroupBy(entry => (entry.Number, Code: entry.Code!.ToUpperInvariant())))
        {
            var ordered = run.OrderBy(entry => entry.Date).ToList();
            var first = ordered[0];
            var last = first;
            foreach (var entry in ordered.Skip(1).Append(null))
            {
                if (entry is not null && entry.Date!.Value.DayNumber - last.Date!.Value.DayNumber <= 1)
                {
                    last = entry;
                    continue;
                }

                var kind = LegacyLeaveCodes.ByShortName.GetValueOrDefault(run.Key.Code);
                if (kind?.Leave is { } leaveKind)
                {
                    leave.Add(new ImportLeaveRow(first.Line, run.Key.Number, leaveKind, first.Date!.Value, last.Date!.Value));
                }
                else
                {
                    // A kind PersoNet does not know (ZO, OP) stays an excused absence with its short name in the note.
                    absences.Add(new ImportAbsenceRow(
                        first.Line, run.Key.Number, kind?.Absence ?? PersoNet.Contracts.Absences.AbsenceKind.OtherExcused, first.Date!.Value, last.Date!.Value,
                        kind?.Absence is null ? $"Kod {run.Key.Code} z poprzedniego systemu" : null));
                }

                if (entry is not null)
                {
                    first = last = entry;
                }
            }
        }

        return (leave.OrderBy(row => row.Line).ToList(), absences.OrderBy(row => row.Line).ToList());
    }

    private string Intern(string number)
    {
        if (!_numbers.TryGetValue(number, out var known))
        {
            _numbers[number] = known = number;
        }

        return known;
    }

    private static Entry ToEntry(SqlRow row)
    {
        var number = Text(row, "idprac") ?? string.Empty;
        DateOnly? date = int.TryParse(Text(row, "dzien"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var day) && day is > 0 and < 80_000
            ? DayZero.AddDays(day)
            : null;
        var code = Text(row, "inne");
        return new Entry(row.Line, number, date, code, Time(Text(row, "od")), Time(Text(row, "do")));
    }

    // Days since day zero with the time as a fraction of a day; a value under one day is no time at all.
    private static TimeOnly? Time(string? value)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || number < 1)
        {
            return null;
        }

        var minutes = (int)Math.Round((number - Math.Floor(number)) * 24 * 60) % (24 * 60);
        return new TimeOnly(minutes / 60, minutes % 60);
    }

    private static string? Text(SqlRow row, string column) =>
        row.Values.TryGetValue(column, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim() : null;

    [GeneratedRegex(@"^\p{Lu}{1,6}$")]
    private static partial Regex ShortName();

    private sealed record Entry(int Line, string Number, DateOnly? Date, string? Code, TimeOnly? Start, TimeOnly? End);
}
