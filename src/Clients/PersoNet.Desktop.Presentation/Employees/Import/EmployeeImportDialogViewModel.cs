using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Employees.Import;

/// <summary>
/// "Import pracowników": HR picks the SQL file of the earlier system, sees every employee found in it and what is wrong
/// with any of them, and only then imports. The file is read on this computer; only the employees are sent to the server.
/// </summary>
public sealed partial class EmployeeImportDialogViewModel(IEmployeesApi api, IFileService files) : DialogViewModel
{
    /// <summary>Employees sent in one request, well under the server's limit.</summary>
    public const int BatchSize = 1000;

    public override string Title => "Import pracowników z pliku SQL";

    public ObservableCollection<EmployeeImportRow> Rows { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFile))]
    public partial string? FileName { get; set; }

    public bool HasFile => FileName is not null;

    /// <summary>What was found, e.g. "Znaleziono 2065 pracowników, 3 z błędami (zostaną pominięte)".</summary>
    [ObservableProperty]
    public partial string? Summary { get; set; }

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
        if (files.AskWhichSqlFileToOpen() is { } path)
        {
            LoadFile(path);
        }
    }

    /// <summary>Reads the file and shows what is in it; nothing is sent yet.</summary>
    public void LoadFile(string path)
    {
        ErrorMessage = null;
        ResultText = null;
        Rows.Clear();
        ReadyCount = 0;
        Summary = null;
        FileName = Path.GetFileName(path);

        IReadOnlyList<EmployeeFileEntry> entries;
        try
        {
            entries = EmployeeSqlImport.Read(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or FormatException)
        {
            ErrorMessage = exception is FormatException ? exception.Message : $"Nie udało się odczytać pliku: {exception.Message}";
            return;
        }

        if (entries.Count == 0)
        {
            ErrorMessage = $"W pliku nie ma danych tabeli {EmployeeSqlImport.Table} (poleceń INSERT ani REPLACE). Sprawdź, czy to eksport tej tabeli.";
            return;
        }

        foreach (var entry in entries)
        {
            Rows.Add(new EmployeeImportRow(entry));
        }

        ReadyCount = entries.Count(entry => entry.Problem is null);
        var withProblems = entries.Count - ReadyCount;
        Summary = withProblems == 0
            ? $"Znaleziono: {Employees(entries.Count)}. Wszystkie można zaimportować."
            : $"Znaleziono: {Employees(entries.Count)}, w tym {withProblems} z błędami. Te zostaną pominięte; resztę można zaimportować.";
    }

    [RelayCommand(CanExecute = nameof(CanImport))]
    public Task ImportAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var ready = Rows.Where(row => row.Entry.Row is not null).ToList();
        var byLine = ready.ToDictionary(row => row.Entry.Line);
        int added = 0, existing = 0, entitlements = 0, rejected = 0;

        foreach (var batch in ready.Chunk(BatchSize))
        {
            var result = await api.ImportAsync(new ImportEmployeesRequest([.. batch.Select(row => row.Entry.Row!)]), cancellationToken);
            added += result.Added;
            existing += result.Existing;
            entitlements += result.EntitlementsAdded;
            rejected += result.Problems.Count;

            foreach (var problem in result.Problems)
            {
                if (byLine.TryGetValue(problem.Line, out var row))
                {
                    row.Reject(problem.Message);
                }
            }

            HasChanges |= result.Added > 0;
        }

        var skipped = Rows.Count - ready.Count + rejected;
        ResultText = string.Create(
            CultureInfo.CurrentCulture,
            $"Zaimportowano: {Employees(added)}, z wymiarem urlopu na ten rok: {entitlements}. "
            + $"Już byli w PersoNet: {existing}. Pominięto z powodu błędów: {skipped}.");
    });

    protected override bool TryShowOnField(ApiException exception) => false;

    private bool CanImport() => ReadyCount > 0 && !IsImported;

    private static string Employees(int count) => count == 1 ? "1 pracownik" : $"{count} pracowników";
}

/// <summary>One employee of the file in the preview.</summary>
public sealed partial class EmployeeImportRow(EmployeeFileEntry entry) : ObservableObject
{
    public EmployeeFileEntry Entry { get; } = entry;

    public int Line => Entry.Line;

    public string Number => Entry.Number;

    public string Name => $"{Entry.LastName} {Entry.FirstName}".Trim();

    public string HireText => Entry.HireDate?.ToString("d", CultureInfo.CurrentCulture) ?? "–";

    public string TerminationText => Entry.TerminationDate?.ToString("d", CultureInfo.CurrentCulture) ?? string.Empty;

    public string LeaveText => Entry.AnnualLeaveDays is { } days ? days.ToString(CultureInfo.CurrentCulture) : string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProblem))]
    public partial string? Problem { get; set; } = entry.Problem;

    public bool HasProblem => Problem is not null;

    public void Reject(string message) => Problem = message;
}
