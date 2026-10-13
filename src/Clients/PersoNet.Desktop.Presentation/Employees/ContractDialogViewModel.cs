using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Employees;

/// <summary>Dialog with the form of a new contract or of the one being changed.</summary>
public sealed partial class ContractDialogViewModel : DialogViewModel
{
    private readonly IEmployeesApi _api;
    private readonly EmployeeResponse _employee;
    private readonly ContractRow? _row;

    public ContractDialogViewModel(IEmployeesApi api, EmployeeResponse employee, ContractRow? row, DateTime start)
    {
        ArgumentNullException.ThrowIfNull(employee);

        _api = api;
        _employee = employee;
        _row = row;

        if (row?.Contract is { } contract)
        {
            EditKind = ContractsViewModel.Kinds.First(kind => kind.Kind == contract.Type);
            EditEndDate = contract.EndDate?.ToDateTime(TimeOnly.MinValue);
            EditWorkingTime = ContractsViewModel.FormatFraction(contract.WorkingTime ?? 1m);
        }

        EditStartDate = start;
        ClearAllErrors();
    }

    public override string Title => IsNew ? $"Nowa umowa – {EmployeeName}" : $"Umowa – {EmployeeName}";

    public string EmployeeName => $"{_employee.FirstName} {_employee.LastName}";

    public bool IsNew => _row is null;

    /// <summary>The contract after a successful save.</summary>
    public ContractResponse? Saved { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmploymentContract), nameof(IsIndefinite), nameof(EndDateLabel), nameof(EndDateHint))]
    public partial ContractKindChoice EditKind { get; set; } = ContractsViewModel.Kinds[0];

    [ObservableProperty]
    public partial DateTime? EditStartDate { get; set; }

    [ObservableProperty]
    public partial DateTime? EditEndDate { get; set; }

    /// <summary>Working time as typed: "1", "0,5", "1/2" or "3/4".</summary>
    [ObservableProperty]
    public partial string EditWorkingTime { get; set; } = "1";

    public bool IsEmploymentContract => EditKind.Kind is ContractKind.Indefinite or ContractKind.FixedTerm or ContractKind.Probation;

    public bool IsIndefinite => EditKind.Kind == ContractKind.Indefinite;

    public string EndDateLabel => EditKind.Kind is ContractKind.FixedTerm or ContractKind.Probation ? "Data _końca *" : "Data _końca";

    public string EndDateHint => EditKind.Kind == ContractKind.Probation
        ? "Okres próbny trwa najwyżej 3 miesiące."
        : "Zostaw puste, jeśli umowa nie ma ustalonego końca.";

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!Validate(out var workingTime))
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
            Saved = _row is { } current
                ? await _api.UpdateContractAsync(_employee.Id, current.Contract.Id, request, cancellationToken)
                : await _api.CreateContractAsync(_employee.Id, request, cancellationToken);

            HasChanges = true;
            Close(true);
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

    // An indefinite contract has no end, so a date left from another kind would only cause an error.
    partial void OnEditKindChanged(ContractKindChoice value)
    {
        if (value.Kind == ContractKind.Indefinite)
        {
            EditEndDate = null;
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
            workingTime = ContractsViewModel.ParseFraction(EditWorkingTime);
            SetError(nameof(EditWorkingTime), workingTime is > 0m and <= 1m ? null : "Wpisz wymiar etatu, np. 1, 1/2 albo 0,75.");
        }
        else
        {
            SetError(nameof(EditWorkingTime), null);
        }

        return !HasErrors;
    }
}
