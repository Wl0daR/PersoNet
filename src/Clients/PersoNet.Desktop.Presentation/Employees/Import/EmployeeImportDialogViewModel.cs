using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Absences;
using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Safety;
using PersoNet.ApiClient.WorkTime;
using PersoNet.Contracts.Absences;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Safety;
using PersoNet.Contracts.WorkTime;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Employees.Import;

/// <summary>
/// "Import pracowników": HR picks the .sql files of the earlier system, chooses the company when the files hold more than
/// one, sees every employee with their department, position and any problem, and only then imports: departments and
/// positions first, then the employees with their contracts, and last their leave history (leave, absences and yearly
/// entitlements), medical examinations and trainings, and attendance (days worked and the absences marked in them) when
/// the files have them. The files are
/// read on this computer; only what is imported is sent to the server.
/// </summary>
public sealed partial class EmployeeImportDialogViewModel(
    IEmployeesApi api, IAbsencesApi absences, IWorkTimeApi workTime, ISafetyApi safety, IFileService files) : DialogViewModel
{
    /// <summary>Problems of the leave history listed in the result; the rest are only counted.</summary>
    public const int ProblemsShown = 5;

    /// <summary>Employees sent in one request, well under the server's limit.</summary>
    public const int BatchSize = 1000;

    /// <summary>How much of a file is looked at to tell whether it holds one of the tables; large files of other tables are skipped.</summary>
    private const int HeadLength = 64 * 1024;

    private EmployeeSqlImport? _import;
    private EmployeeImportPlan? _plan;
    private AttendanceSqlImport? _attendance;
    private AttendancePlan _attendancePlan = AttendancePlan.None;
    private SafetySqlImport? _safety;
    private SafetyPlan _safetyPlan = SafetyPlan.None;

    public override string Title => "Import pracowników z bazy SQL";

    public ObservableCollection<EmployeeImportRow> Rows { get; } = [];

    public ObservableCollection<LegacyEmployer> Employers { get; } = [];

    /// <summary>The files read, e.g. "table-Employees.sql, table-Departments.sql".</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    public partial string? FileName { get; set; }

    public bool HasFile => FileName is not null;

    /// <summary>The company whose people are imported, when the files hold more than one.</summary>
    [ObservableProperty]
    public partial LegacyEmployer? Employer { get; set; }

    public bool HasEmployers => Employers.Count > 1;

    /// <summary>What was found, e.g. "Znaleziono 2065 pracowników, 3 z błędami (zostaną pominięte)".</summary>
    [ObservableProperty]
    public partial string? Summary { get; set; }

    /// <summary>What else the files hold, e.g. "Działy: 52, stanowiska: 180, umowy: 1990".</summary>
    [ObservableProperty]
    public partial string? Details { get; set; }

    /// <summary>What the import did; null until it ran.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsImported))]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    public partial string? ResultText { get; set; }

    public bool IsImported => ResultText is not null;

    /// <summary>How far a long import has got, e.g. "Historia nieobecności: 12 000 z 82 000 wierszy"; null otherwise.</summary>
    [ObservableProperty]
    public partial string? Progress { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    public partial int ReadyCount { get; set; }

    /// <summary>Asks for the files and reads them off the window's thread: a dump of attendance takes half a minute.</summary>
    [RelayCommand]
    public async Task ChooseFileAsync()
    {
        if (files.AskWhichSqlFilesToOpen() is not { Count: > 0 } paths)
        {
            return;
        }

        Clear(paths);
        IsBusy = true;
        Progress = "Wczytywanie plików…";
        try
        {
            Show(await Task.Run(() => Read(paths)));
        }
        finally
        {
            Progress = null;
            IsBusy = false;
        }
    }

    public void LoadFile(string path) => LoadFiles([path]);

    /// <summary>Reads the files and shows what is in them; nothing is sent yet.</summary>
    public void LoadFiles(IReadOnlyList<string> paths)
    {
        Clear(paths);
        Show(Read(paths));
    }

    private void Clear(IReadOnlyList<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);

        ErrorMessage = null;
        ResultText = null;
        Rows.Clear();
        Employers.Clear();
        ReadyCount = 0;
        Summary = null;
        Details = null;
        _import = null;
        _plan = null;
        _attendance = null;
        _attendancePlan = AttendancePlan.None;
        _safety = null;
        _safetyPlan = SafetyPlan.None;
        FileName = string.Join(", ", paths.Select(Path.GetFileName));
    }

    // Touches nothing on the screen, so it can run off the window's thread.
    private static Loaded Read(IReadOnlyList<string> paths)
    {
        var dumps = new List<string>();
        var safetyDumps = new List<string>();
        AttendanceSqlImport? attendance = null;
        try
        {
            foreach (var path in paths)
            {
                var head = Head(path);
                if (Declares(head, AttendanceSqlImport.Table))
                {
                    // About a million rows: read line by line rather than as one string.
                    using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                    attendance = AttendanceSqlImport.Read(reader);
                }
                else
                {
                    var employees = EmployeeSqlImport.Tables.Any(table => Declares(head, table));
                    var safety = SafetySqlImport.Tables.Any(table => Declares(head, table));
                    var text = employees || safety ? File.ReadAllText(path) : null;
                    if (employees)
                    {
                        dumps.Add(text!);
                    }

                    if (safety)
                    {
                        safetyDumps.Add(text!);
                    }
                }
            }

            return new Loaded(EmployeeSqlImport.Read(dumps), attendance, safetyDumps.Count > 0 ? SafetySqlImport.Read(safetyDumps) : null, null);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            return new Loaded(null, null, null, exception is FormatException ? exception.Message : $"Nie udało się odczytać pliku: {exception.Message}");
        }
    }

    private void Show(Loaded loaded)
    {
        if (loaded.Error is not null)
        {
            ErrorMessage = loaded.Error;
            return;
        }

        _import = loaded.Import!;
        _attendance = loaded.Attendance;
        _safety = loaded.Safety;
        if (!_import.HasEmployees)
        {
            ErrorMessage = $"W wybranych plikach nie ma danych tabeli {EmployeeSqlImport.Table} (poleceń INSERT ani REPLACE). Zaznacz także plik z tą tabelą.";
            return;
        }

        foreach (var employer in _import.Employers)
        {
            Employers.Add(employer);
        }

        OnPropertyChanged(nameof(HasEmployers));
        Employer = Employers[0];
    }

    partial void OnEmployerChanged(LegacyEmployer? value)
    {
        if (_import is null || value is null)
        {
            return;
        }

        _plan = _import.Plan(value);
        _attendancePlan = _attendance?.Plan(_plan.Employees.Select(entry => entry.Number.Trim().ToUpperInvariant()).ToHashSet(StringComparer.Ordinal))
            ?? AttendancePlan.None;
        _safetyPlan = _safety?.Plan(_plan) ?? SafetyPlan.None;
        ResultText = null;
        Rows.Clear();
        foreach (var entry in _plan.Employees)
        {
            Rows.Add(new EmployeeImportRow(entry));
        }

        ReadyCount = _plan.Employees.Count(entry => entry.Problem is null);
        var withProblems = _plan.Employees.Count - ReadyCount;
        Summary = withProblems == 0
            ? $"Znaleziono: {Employees(_plan.Employees.Count)}. Wszystkich można zaimportować."
            : $"Znaleziono: {Employees(_plan.Employees.Count)}, w tym {withProblems} z błędami. Ci zostaną pominięci; resztę można zaimportować.";

        var details = new List<string>
        {
            $"działy: {_plan.Departments.Count}",
            $"stanowiska: {_plan.Positions.Count}",
            $"okresy zatrudnienia (umowy): {_plan.Employees.Sum(entry => entry.Contracts?.Count ?? 0)}",
        };
        if (_plan.OtherEmployersEmployees > 0)
        {
            details.Add($"pracownicy innych firm, pominięci: {_plan.OtherEmployersEmployees}");
        }

        var history = _plan.LeaveHistory;
        if (!history.IsEmpty || history.Problems.Count > 0)
        {
            details.Add($"urlopy: {history.Leave.Count}, nieobecności: {history.Absences.Count}, wymiar roczny: {history.Entitlements.Count}");
            if (history.Problems.Count > 0)
            {
                details.Add($"wiersze nieobecności z błędami: {history.Problems.Count}");
            }
        }

        var attendance = _attendancePlan;
        if (_attendance is not null)
        {
            details.Add(string.Create(CultureInfo.CurrentCulture,
                $"z obecności dni pracy: {attendance.Days.Count:N0}, zwolnienia płatne: {attendance.Leave.Count}, nieobecności: {attendance.Absences.Count}"));
            if (attendance.Problems.Count > 0)
            {
                details.Add($"wiersze obecności z błędami: {attendance.Problems.Count}");
            }
        }

        var records = _safetyPlan;
        if (_safety is not null)
        {
            details.Add($"lista szkoleń: {records.Courses.Count}, ukończone szkolenia: {records.Trainings.Count}, badania lekarskie: {records.Exams.Count}");
            if (records.Problems.Count > 0)
            {
                details.Add($"wiersze badań i szkoleń z błędami: {records.Problems.Count}");
            }
        }

        IEnumerable<string> tables = _import.FoundTables;
        tables = _safety is null ? tables : tables.Concat(_safety.FoundTables);
        tables = _attendance is null ? tables : tables.Append(AttendanceSqlImport.Table);
        Details = string.Create(CultureInfo.CurrentCulture, $"Wczytane tabele: {string.Join(", ", tables)}. Do zaimportowania {string.Join(", ", details)}.");
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    public Task ImportAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var plan = _plan!;
        var text = new StringBuilder();

        if (plan.Departments.Count + plan.Positions.Count > 0)
        {
            var organization = await api.ImportOrganizationAsync(new ImportOrganizationRequest(plan.Departments, plan.Positions), cancellationToken);
            text.Append(CultureInfo.CurrentCulture, $"Nowe działy: {organization.DepartmentsAdded}, nowe stanowiska: {organization.PositionsAdded}. ");
            HasChanges |= organization.DepartmentsAdded + organization.PositionsAdded > 0;
        }

        var ready = Rows.Where(row => row.Entry.Row is not null).ToList();
        var byLine = ready.ToDictionary(row => row.Entry.Line);
        int added = 0, updated = 0, existing = 0, entitlements = 0, contracts = 0, rejected = 0;

        foreach (var batch in ready.Chunk(BatchSize))
        {
            var result = await api.ImportAsync(new ImportEmployeesRequest([.. batch.Select(row => row.Entry.Row!)]), cancellationToken);
            added += result.Added;
            updated += result.Updated;
            existing += result.Existing;
            entitlements += result.EntitlementsAdded;
            contracts += result.ContractsAdded;

            // A row can have a note (a department that is missing) and still be imported; only a row that is not is skipped.
            foreach (var problem in result.Problems)
            {
                if (byLine.TryGetValue(problem.Line, out var row))
                {
                    row.Reject(problem.Message);
                }
            }

            rejected += result.Received - result.Added - result.Updated - result.Existing;
            HasChanges |= result.Added + result.Updated > 0;
        }

        var skipped = Rows.Count - ready.Count + rejected;
        text.Append(CultureInfo.CurrentCulture, $"Zaimportowano: {Employees(added)}, z wymiarem urlopu na ten rok: {entitlements}. ");
        if (updated > 0)
        {
            text.Append(CultureInfo.CurrentCulture, $"Uzupełniono dane już obecnych: {updated}. ");
        }

        text.Append(CultureInfo.CurrentCulture, $"Umowy: {contracts}. Bez zmian: {existing}. Pominięto z powodu błędów: {skipped}.");

        if (!plan.LeaveHistory.IsEmpty)
        {
            text.AppendLine().AppendLine().Append(await ImportHistoryAsync(plan.LeaveHistory, cancellationToken));
        }

        if (!_safetyPlan.IsEmpty)
        {
            text.AppendLine().AppendLine().Append(await ImportSafetyAsync(_safetyPlan, cancellationToken));
        }

        if (!_attendancePlan.IsEmpty)
        {
            text.AppendLine().AppendLine().Append(await ImportAttendanceAsync(_attendancePlan, cancellationToken));
        }

        ResultText = text.ToString();
    });

    // After the employees, as the history is matched to them by number; in small batches, as each row is a record of its own.
    private async Task<string> ImportHistoryAsync(LeaveHistoryPlan history, CancellationToken cancellationToken)
    {
        int leave = 0, absent = 0, entitlements = 0, existing = 0, sent = 0;
        var total = history.Leave.Count + history.Absences.Count + history.Entitlements.Count;
        var problems = new List<ImportLeaveProblem>(history.Problems);
        var batches = history.Leave.Chunk(BatchSize).Select(batch => new ImportLeaveHistoryRequest(batch, [], []))
            .Concat(history.Absences.Chunk(BatchSize).Select(batch => new ImportLeaveHistoryRequest([], batch, [])))
            .Concat(history.Entitlements.Chunk(BatchSize).Select(batch => new ImportLeaveHistoryRequest([], [], batch)));

        foreach (var batch in batches)
        {
            Progress = string.Create(CultureInfo.CurrentCulture, $"Historia nieobecności: {sent:N0} z {total:N0} wierszy…");
            var result = await absences.ImportHistoryAsync(batch, cancellationToken);
            sent += batch.Leave.Count + batch.Absences.Count + batch.Entitlements.Count;
            leave += result.LeaveAdded;
            absent += result.AbsencesAdded;
            entitlements += result.EntitlementsSaved;
            existing += result.Existing;
            problems.AddRange(result.Problems);
            HasChanges |= result.LeaveAdded + result.AbsencesAdded + result.EntitlementsSaved > 0;
        }

        Progress = null;

        var text = new StringBuilder();
        text.Append(CultureInfo.CurrentCulture, $"Historia: urlopy {leave}, nieobecności {absent}, wymiar roczny (lata pracowników) {entitlements}. Już były w PersoNet: {existing}.");
        AppendProblems(text, problems);
        return text.ToString();
    }

    // The list of trainings first, as the completed trainings are matched to it by name.
    private async Task<string> ImportSafetyAsync(SafetyPlan plan, CancellationToken cancellationToken)
    {
        int courses = 0, trainings = 0, exams = 0, existing = 0, sent = 0;
        var total = plan.Courses.Count + plan.Trainings.Count + plan.Exams.Count;
        var problems = new List<ImportLeaveProblem>(plan.Problems);
        var batches = plan.Courses.Chunk(BatchSize).Select(batch => new ImportSafetyRequest(batch, [], []))
            .Concat(plan.Trainings.Chunk(BatchSize).Select(batch => new ImportSafetyRequest([], batch, [])))
            .Concat(plan.Exams.Chunk(BatchSize).Select(batch => new ImportSafetyRequest([], [], batch)));

        foreach (var batch in batches)
        {
            Progress = string.Create(CultureInfo.CurrentCulture, $"Badania i szkolenia: {sent:N0} z {total:N0} wierszy…");
            var result = await safety.ImportAsync(batch, cancellationToken);
            sent += batch.Courses.Count + batch.Trainings.Count + batch.Exams.Count;
            courses += result.CoursesAdded;
            trainings += result.TrainingsAdded;
            exams += result.ExamsAdded;
            existing += result.Existing;
            problems.AddRange(result.Problems);
            HasChanges |= result.CoursesAdded + result.TrainingsAdded + result.ExamsAdded > 0;
        }

        Progress = null;

        var text = new StringBuilder();
        text.Append(CultureInfo.CurrentCulture, $"Badania i szkolenia: lista szkoleń {courses}, ukończone szkolenia {trainings}, badania lekarskie {exams}. Już były w PersoNet: {existing}.");
        AppendProblems(text, problems);
        return text.ToString();
    }

    // The marked absences first, so a day worked that clashes with leave already in PersoNet is reported, not recorded.
    private async Task<string> ImportAttendanceAsync(AttendancePlan attendance, CancellationToken cancellationToken)
    {
        int leave = 0, absent = 0, days = 0, existing = 0, sent = 0;
        var total = attendance.Leave.Count + attendance.Absences.Count + attendance.Days.Count;
        var problems = new List<ImportLeaveProblem>(attendance.Problems);

        foreach (var batch in attendance.Leave.Chunk(BatchSize).Select(batch => new ImportLeaveHistoryRequest(batch, [], []))
            .Concat(attendance.Absences.Chunk(BatchSize).Select(batch => new ImportLeaveHistoryRequest([], batch, []))))
        {
            Progress = string.Create(CultureInfo.CurrentCulture, $"Obecności: {sent:N0} z {total:N0} wierszy…");
            var result = await absences.ImportHistoryAsync(batch, cancellationToken);
            sent += batch.Leave.Count + batch.Absences.Count;
            leave += result.LeaveAdded;
            absent += result.AbsencesAdded;
            existing += result.Existing;
            problems.AddRange(result.Problems);
            HasChanges |= result.LeaveAdded + result.AbsencesAdded > 0;
        }

        foreach (var batch in attendance.Days.Chunk(BatchSize))
        {
            Progress = string.Create(CultureInfo.CurrentCulture, $"Obecności: {sent:N0} z {total:N0} wierszy…");
            var result = await workTime.ImportDaysAsync(new ImportWorkDaysRequest(batch), cancellationToken);
            sent += batch.Length;
            days += result.Added;
            existing += result.Existing;
            problems.AddRange(result.Problems);
            HasChanges |= result.Added > 0;
        }

        Progress = null;

        var text = new StringBuilder();
        text.Append(CultureInfo.CurrentCulture, $"Obecności: dni pracy {days:N0}, zwolnienia płatne {leave}, nieobecności {absent}. Już były w PersoNet: {existing:N0}.");
        AppendProblems(text, problems);
        return text.ToString();
    }

    private static void AppendProblems(StringBuilder text, List<ImportLeaveProblem> problems)
    {
        if (problems.Count == 0)
        {
            return;
        }

        text.Append(CultureInfo.CurrentCulture, $" Nie zaimportowano wierszy: {problems.Count}, np.:");
        foreach (var problem in problems.OrderBy(problem => problem.Line).Take(ProblemsShown))
        {
            var who = string.IsNullOrEmpty(problem.Number) ? string.Empty : $", pracownik {problem.Number}";
            text.AppendLine().Append(CultureInfo.CurrentCulture, $"• wiersz {problem.Line}{who}: {problem.Message}");
        }
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    private bool CanImport() => ReadyCount > 0 && !IsImported;

    private static string Employees(int count) => count == 1 ? "1 pracownik" : $"{count} pracowników";

    // A dump names its table at the top, so the head tells which reader a file needs (and skips files of other tables).
    private static string Head(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[HeadLength];
        return new string(buffer, 0, reader.ReadBlock(buffer, 0, buffer.Length));
    }

    private static bool Declares(string head, string table) =>
        Regex.IsMatch(head, $@"(CREATE\s+TABLE|INSERT|REPLACE)[^;(]*?`?{Regex.Escape(table)}`?\s*[(\s]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private sealed record Loaded(EmployeeSqlImport? Import, AttendanceSqlImport? Attendance, SafetySqlImport? Safety, string? Error);
}

/// <summary>One employee of the file in the preview.</summary>
public sealed partial class EmployeeImportRow(EmployeeFileEntry entry) : ObservableObject
{
    public EmployeeFileEntry Entry { get; } = entry;

    public int Line => Entry.Line;

    public string Number => Entry.Number;

    public string Name => $"{Entry.LastName} {Entry.FirstName}".Trim();

    public string Department => Entry.DepartmentName ?? string.Empty;

    public string Position => Entry.Position ?? string.Empty;

    public string HireText => Entry.HireDate?.ToString("d", CultureInfo.CurrentCulture) ?? "–";

    public string TerminationText => Entry.TerminationDate?.ToString("d", CultureInfo.CurrentCulture) ?? string.Empty;

    public string LeaveText => Entry.AnnualLeaveDays is { } days ? days.ToString(CultureInfo.CurrentCulture) : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; set; } = entry.Problem;

    public bool HasProblem => Problem is not null;

    public void Reject(string message) => Problem = message;
}
