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
/// "Umowy" tab of an employee's card: the contracts, newest first, and the editor of the selected or new one.
/// </summary>
public sealed partial class ContractsViewModel(IEmployeesApi api, TimeProvider time) : EditorViewModel
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
    [NotifyPropertyChangedFor(nameof(IsNew), nameof(EditorTitle))]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand))]
    public partial ContractRow? Selected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmploymentContract), nameof(IsIndefinite), nameof(EndDateLabel), nameof(EndDateHint))]
    public partial ContractKindChoice EditKind { get; set; } = Kinds[0];

    [ObservableProperty]
    public partial DateTime? EditStartDate { get; set; }

    [ObservableProperty]
    public partial DateTime? EditEndDate { get; set; }

    /// <summary>Working time as typed: "1", "0,5", "1/2" or "3/4".</summary>
    [ObservableProperty]
    public partial string EditWorkingTime { get; set; } = "1";

    /// <summary>First click on "Usuń" asks for confirmation; the second one deletes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteText))]
    public partial bool IsConfirmingDelete { get; set; }

    public bool IsNew => Selected is null;

    public string EditorTitle => IsNew ? "Nowa umowa" : "Edycja umowy";

    public bool IsEmploymentContract => EditKind.Kind is ContractKind.Indefinite or ContractKind.FixedTerm or ContractKind.Probation;

    public bool IsIndefinite => EditKind.Kind == ContractKind.Indefinite;

    public string EndDateLabel => EditKind.Kind is ContractKind.FixedTerm or ContractKind.Probation ? "Data _końca *" : "Data _końca";

    public string EndDateHint => EditKind.Kind == ContractKind.Probation
        ? "Okres próbny trwa najwyżej 3 miesiące."
        : "Zostaw puste, jeśli umowa nie ma ustalonego końca.";

    public string DeleteText => IsConfirmingDelete ? "Na pewno usunąć?" : "Usuń umowę";

    /// <summary>Shows the contracts of the employee (or nothing for a card that is not saved yet).</summary>
    public async Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        var version = ++_loadVersion;

        Items.Clear();
        IsEmpty = employee is not null;
        StartNew();

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

    [RelayCommand]
    public void New()
    {
        Selected = null;
        StartNew();
    }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee || !Validate(out var workingTime))
        {
            return Task.CompletedTask;
        }

        var request = new SaveContractRequest(
            EditKind.Kind,
            DateOnly.FromDateTime(EditStartDate!.Value),
            EditEndDate is { } end ? DateOnly.FromDateTime(end) : null,
            workingTime);

        return RunAsync(async () =>
        {
            var saved = Selected is { } current
                ? await api.UpdateContractAsync(employee.Id, current.Contract.Id, request, cancellationToken)
                : await api.CreateContractAsync(employee.Id, request, cancellationToken);

            Fill(await api.ListContractsAsync(employee.Id, cancellationToken), saved.Id);
        });
    }

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

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "contract.overlap" or "contract.before_hire" => nameof(EditStartDate),
            "contract.end_required" or "contract.no_end_for_indefinite" or "contract.end_before_start" or "contract.probation_too_long" => nameof(EditEndDate),
            "contract.working_time_invalid" => nameof(EditWorkingTime),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }

    partial void OnSelectedChanged(ContractRow? value)
    {
        IsConfirmingDelete = false;

        if (value is null)
        {
            return;
        }

        var contract = value.Contract;
        EditKind = Kinds.First(kind => kind.Kind == contract.Type);
        EditStartDate = contract.StartDate.ToDateTime(TimeOnly.MinValue);
        EditEndDate = contract.EndDate?.ToDateTime(TimeOnly.MinValue);
        EditWorkingTime = FormatFraction(contract.WorkingTime ?? 1m);
        ClearAllErrors();
    }

    // An indefinite contract has no end, so a date left from another kind would only cause an error.
    partial void OnEditKindChanged(ContractKindChoice value)
    {
        if (value.Kind == ContractKind.Indefinite)
        {
            EditEndDate = null;
        }
    }

    private bool CanDelete() => Selected is not null;

    private void StartNew()
    {
        Selected = null;
        EditKind = Kinds[0];
        EditStartDate = _employee is null ? time.GetLocalNow().Date : LatestEnd() ?? _employee.HireDate.ToDateTime(TimeOnly.MinValue);
        EditEndDate = null;
        EditWorkingTime = "1";
        ClearAllErrors();
    }

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

        var selected = Items.FirstOrDefault(row => row.Contract.Id == selectId);
        if (selected is null)
        {
            StartNew();
        }
        else
        {
            Selected = selected;
        }
    }

    private bool Validate(out decimal? workingTime)
    {
        workingTime = null;

        SetError(nameof(EditStartDate), EditStartDate is null ? "Podaj datę początku umowy." : null);

        var needsEnd = EditKind.Kind is ContractKind.FixedTerm or ContractKind.Probation;
        SetError(nameof(EditEndDate), needsEnd && EditEndDate is null ? "Podaj datę końca umowy." : null);

        if (IsEmploymentContract)
        {
            workingTime = ParseFraction(EditWorkingTime);
            SetError(nameof(EditWorkingTime), workingTime is > 0m and <= 1m ? null : "Wpisz wymiar etatu, np. 1, 1/2 albo 0,75.");
        }
        else
        {
            SetError(nameof(EditWorkingTime), null);
        }

        return !HasErrors;
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

    private static string FormatFraction(decimal value) => value switch
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
