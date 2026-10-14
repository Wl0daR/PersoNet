using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Reports;

/// <summary>
/// One report of the "Raporty" screen: its parameters, the table on screen and the export of that table to Excel. The
/// export writes what the screen shows, so the file always matches the last loaded report.
/// </summary>
public abstract partial class ReportViewModel(IFileService files) : EditorViewModel
{
    protected static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private int _loadVersion;

    /// <summary>Name on the report's tab.</summary>
    public abstract string Name { get; }

    /// <summary>What the report shows, in one sentence under its name.</summary>
    public abstract string Description { get; }

    /// <summary>The totals of the loaded report, e.g. "42 osoby, 39,5 etatu".</summary>
    [ObservableProperty]
    public partial string Summary { get; protected set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ExportCommand))]
    public partial bool IsEmpty { get; protected set; } = true;

    /// <summary>The file saved last, which "Otwórz plik" opens; null before the first export.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExport))]
    [NotifyCanExecuteChangedFor(nameof(OpenExportCommand))]
    public partial string? ExportPath { get; private set; }

    public bool HasExport => ExportPath is not null;

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var version = ++_loadVersion;
        await LoadReportAsync(() => version == _loadVersion, cancellationToken);
    });

    /// <summary>Saves the report shown on screen to an Excel workbook the user picks, ready to open.</summary>
    [RelayCommand(CanExecute = nameof(CanExport))]
    public void Export()
    {
        if (files.AskWhereToSaveWorkbook(FileName) is not { } path)
        {
            return;
        }

        ErrorMessage = null;
        try
        {
            using (var stream = File.Create(path))
            {
                ExcelExport.Write(stream, Tables());
            }

            ExportPath = path;
        }
        catch (IOException exception)
        {
            ErrorMessage = $"Nie udało się zapisać pliku: {exception.Message} Zamknij go w Excelu, jeśli jest otwarty, i spróbuj ponownie.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "Nie masz prawa zapisu w tym miejscu. Wybierz inny folder.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasExport))]
    public void OpenExport()
    {
        if (ExportPath is { } path)
        {
            files.Open(path);
        }
    }

    /// <summary>Loads the report; <paramref name="isCurrent"/> turns false when a newer load has started meanwhile.</summary>
    protected abstract Task LoadReportAsync(Func<bool> isCurrent, CancellationToken cancellationToken);

    /// <summary>Suggested file name, e.g. "Stan zatrudnienia 2025-06-30.xlsx".</summary>
    protected abstract string FileName { get; }

    /// <summary>The sheets of the export.</summary>
    protected abstract IReadOnlyList<ReportTable> Tables();

    protected override bool TryShowOnField(ApiException exception) => false;

    protected static string LongDate(DateOnly date) => date.ToString("d MMMM yyyy", Polish);

    protected static string FileDate(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>"1 osoba", "3 osoby", "12 osób".</summary>
    protected static string People(int count) => Plural(count, "osoba", "osoby", "osób");

    /// <summary>The Polish plural for a count: one, a few (2–4, 22–24…), many.</summary>
    protected static string Plural(int count, string one, string few, string many)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        var word = count == 1 ? one : last is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? few : many;
        return $"{count} {word}";
    }

    private bool CanExport() => !IsEmpty;
}
