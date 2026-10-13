using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Employees;

/// <summary>A contract kind with its Polish name, for the drop-down list.</summary>
public sealed record ContractKindChoice(ContractKind Kind, string Name);

/// <summary>One contract as shown in the list.</summary>
public sealed record ContractRow(ContractResponse Contract, string TypeName, string Period, string? WorkingTimeText, bool IsCurrent);

/// <summary>
/// "Umowy" tab of an employee's card: the contracts, newest first. A contract is added or changed in a dialog
/// ("Dodaj umowę", or a double click on the contract); removal needs a second click.
/// </summary>
public sealed partial class ContractsViewModel(IEmployeesApi api, TimeProvider time, IDialogService dialogs) : EditorViewModel
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private EmployeeResponse? _employee;
    private int _loadVersion;

    public static IReadOnlyList<ContractKindChoice> Kinds { get; } =
    [
        new(ContractKind.Indefinite, "Umowa o pracę na czas nieokreślony"),
        new(ContractKind.FixedTerm, "Umowa o pracę na czas określony"),
        new(ContractKind.Probation, "Umowa o pracę na okres próbny"),
        new(ContractKind.Mandate, "Umowa zlecenie"),
        new(ContractKind.SpecificWork, "Umowa o dzieło"),
        new(ContractKind.B2B, "Współpraca B2B"),
    ];

    public ObservableCollection<ContractRow> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(EditCommand))]
    public partial ContractRow? Selected { get; set; }

    /// <summary>First click on "Usuń" asks for confirmation; the second one deletes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteText))]
    public partial bool IsConfirmingDelete { get; set; }

    public string DeleteText => IsConfirmingDelete ? "Na pewno usunąć?" : "Usuń umowę";

    /// <summary>Shows the contracts of the employee (or nothing for a card that is not saved yet).</summary>
    public async Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        var version = ++_loadVersion;

        Items.Clear();
        IsEmpty = false;
        Selected = null;

        if (employee is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var contracts = await api.ListContractsAsync(employee.Id, cancellationToken);

            // A newer employee may have been opened while this one loaded.
            if (version == _loadVersion)
            {
                Fill(contracts, selectId: null);
            }
        });
    }

    /// <summary>Opens the form of a new contract; a follow-up contract starts the day after the last one ends.</summary>
    [RelayCommand]
    public Task NewAsync(CancellationToken cancellationToken) => _employee is not { } employee
        ? Task.CompletedTask
        : EditInDialogAsync(new ContractDialogViewModel(api, employee, row: null, LatestEnd() ?? employee.HireDate.ToDateTime(TimeOnly.MinValue)), cancellationToken);

    /// <summary>Opens the picked contract in the form.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    public Task EditAsync(ContractRow? row, CancellationToken cancellationToken) => _employee is not { } employee || (row ?? Selected) is not { } contract
        ? Task.CompletedTask
        : EditInDialogAsync(new ContractDialogViewModel(api, employee, contract, contract.Contract.StartDate.ToDateTime(TimeOnly.MinValue)), cancellationToken);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee || Selected is not { } current)
        {
            return Task.CompletedTask;
        }

        if (!IsConfirmingDelete)
        {
            IsConfirmingDelete = true;
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            await api.DeleteContractAsync(employee.Id, current.Contract.Id, cancellationToken);
            Fill(await api.ListContractsAsync(employee.Id, cancellationToken), selectId: null);
        });
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    partial void OnSelectedChanged(ContractRow? value) => IsConfirmingDelete = false;

    private async Task EditInDialogAsync(ContractDialogViewModel dialog, CancellationToken cancellationToken)
    {
        if (_employee is not { } employee || !await dialogs.ShowAsync(dialog))
        {
            return;
        }

        await RunAsync(async () => Fill(await api.ListContractsAsync(employee.Id, cancellationToken), dialog.Saved?.Id));
    }

    private bool CanEdit(ContractRow? row) => (row ?? Selected) is not null;

    private bool CanDelete() => Selected is not null;

    // A follow-up contract usually starts the day after the last one ends.
    private DateTime? LatestEnd() =>
        Items.Select(row => row.Contract.EndDate).Where(end => end is not null).Max() is { } end
            ? end.AddDays(1).ToDateTime(TimeOnly.MinValue)
            : null;

    private void Fill(IReadOnlyList<ContractResponse> contracts, Guid? selectId)
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().Date);

        Items.Clear();
        foreach (var contract in contracts)
        {
            Items.Add(ToRow(contract, today));
        }

        IsEmpty = Items.Count == 0;
        Selected = Items.FirstOrDefault(row => row.Contract.Id == selectId);
    }

    /// <summary>Reads working time typed as "1", "1/2" or "0,75"; null when it is not a number.</summary>
    public static decimal? ParseFraction(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var value = text.Trim().Replace(',', '.');
        var slash = value.IndexOf('/', StringComparison.Ordinal);

        if (slash > 0
            && decimal.TryParse(value[..slash], NumberStyles.Number, CultureInfo.InvariantCulture, out var numerator)
            && decimal.TryParse(value[(slash + 1)..], NumberStyles.Number, CultureInfo.InvariantCulture, out var denominator)
            && denominator != 0)
        {
            return Math.Round(numerator / denominator, 3);
        }

        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    internal static string FormatFraction(decimal value) => value switch
    {
        1m => "1",
        0.5m => "1/2",
        0.25m => "1/4",
        0.75m => "3/4",
        _ => value.ToString("0.###", Polish),
    };

    private static ContractRow ToRow(ContractResponse contract, DateOnly today)
    {
        var typeName = Kinds.First(kind => kind.Kind == contract.Type).Name;
        var from = contract.StartDate.ToString("d MMM yyyy", Polish);
        var period = contract.EndDate is { } end ? $"{from} – {end.ToString("d MMM yyyy", Polish)}" : $"od {from}, bez daty końca";
        string? workingTime = contract.WorkingTime switch
        {
            null => null,
            1m => "pełny etat",
            { } fraction => $"{FormatFraction(fraction)} etatu",
        };
        var isCurrent = contract.StartDate <= today && (contract.EndDate is null || today <= contract.EndDate);

        return new ContractRow(contract, typeName, period, workingTime, isCurrent);
    }
}
