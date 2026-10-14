using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Employees.Import;

/// <summary>
/// "Import pracowników": HR picks the .sql files of the earlier system, chooses the company when the files hold more than
/// one, sees every employee with their department, position and any problem, and only then imports: departments and
/// positions first, then the employees with their contracts. The files are read on this computer; only what is imported
/// is sent to the server.
/// </summary>
public sealed partial class EmployeeImportDialogViewModel(IEmployeesApi api, IFileService files) : DialogViewModel
{
    /// <summary>Employees sent in one request, well under the server's limit.</summary>
    public const int BatchSize = 1000;

    /// <summary>How much of a file is looked at to tell whether it holds one of the tables; large files of other tables are skipped.</summary>
    private const int HeadLength = 64 * 1024;

    private EmployeeSqlImport? _import;
    private EmployeeImportPlan? _plan;

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

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImportCommand))]
    public partial int ReadyCount { get; set; }

    [RelayCommand]
    public void ChooseFile()
    {
        if (files.AskWhichSqlFilesToOpen() is { Count: > 0 } paths)
        {
            LoadFiles(paths);
        }
    }

    public void LoadFile(string path) => LoadFiles([path]);

    /// <summary>Reads the files and shows what is in them; nothing is sent yet.</summary>
    public void LoadFiles(IReadOnlyList<string> paths)
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
        FileName = string.Join(", ", paths.Select(Path.GetFileName));

        var dumps = new List<string>();
        try
        {
            foreach (var path in paths.Where(HoldsKnownTable))
            {
                dumps.Add(File.ReadAllText(path));
            }

            _import = EmployeeSqlImport.Read(dumps);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            ErrorMessage = exception is FormatException ? exception.Message : $"Nie udało się odczytać pliku: {exception.Message}";
            return;
        }

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

        Details = string.Create(CultureInfo.CurrentCulture, $"Wczytane tabele: {string.Join(", ", _import.FoundTables)}. Do zaimportowania {string.Join(", ", details)}.");
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
        ResultText = text.ToString();
    });

    protected override bool TryShowOnField(ApiException exception) => false;

    private bool CanImport() => ReadyCount > 0 && !IsImported;

    private static string Employees(int count) => count == 1 ? "1 pracownik" : $"{count} pracowników";

    // A dump names its table at the top; reading the head keeps a 150 MB file of attendance out of memory.
    private static bool HoldsKnownTable(string path)
    {
        using var reader = new StreamReader(path, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var buffer = new char[HeadLength];
        var head = new string(buffer, 0, reader.ReadBlock(buffer, 0, buffer.Length));
        return EmployeeSqlImport.Tables.Any(table =>
            Regex.IsMatch(head, $@"(CREATE\s+TABLE|INSERT|REPLACE)[^;(]*?`?{Regex.Escape(table)}`?\s*[(\s]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));
    }
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
