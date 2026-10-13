using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Leave;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Leave;

namespace PersoNet.Desktop.Presentation.Employees;

/// <summary>
/// Employee card, in a window of its own: the personal and employment data ("Dane"), the contracts and the leave.
/// A new card has only the data tab until it is saved.
/// </summary>
public sealed partial class EmployeeCardViewModel : DialogViewModel
{
    public const int NumberMaxLength = 20;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 30;

    public static readonly Choice NoDepartment = new(null, "(bez działu)");
    public static readonly Choice NoPosition = new(null, "(bez stanowiska)");
    public static readonly Choice NoManager = new(null, "(bez przełożonego)");

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly IEmployeesApi _employees;
    private readonly IOrganizationApi _organization;
    private readonly TimeProvider _time;
    private List<EmployeeLookupItem> _managerCandidates = [];

    public EmployeeCardViewModel(IEmployeesApi employees, IOrganizationApi organization, ILeaveApi leave, TimeProvider time, IDialogService dialogs)
    {
        _employees = employees;
        _organization = organization;
        _time = time;
        Contracts = new ContractsViewModel(employees, time, dialogs);
        Leave = new EmployeeLeaveViewModel(leave, time, dialogs);
    }

    /// <summary>"Umowy" tab.</summary>
    public ContractsViewModel Contracts { get; }

    /// <summary>"Urlop" tab.</summary>
    public EmployeeLeaveViewModel Leave { get; }

    public ObservableCollection<Choice> Departments { get; } = [NoDepartment];

    public ObservableCollection<Choice> Positions { get; } = [NoPosition];

    public ObservableCollection<Choice> Managers { get; } = [NoManager];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew), nameof(Title), nameof(EditorTitle), nameof(IsTerminated), nameof(IsEmployed), nameof(StatusText), nameof(StatusBadge), nameof(CardInitials), nameof(CardSubtitle))]
    [NotifyCanExecuteChangedFor(nameof(TerminateCommand), nameof(ReinstateCommand))]
    public partial EmployeeResponse? Current { get; set; }

    [ObservableProperty]
    public partial string EditNumber { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditFirstName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditLastName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditEmail { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditPhone { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime? EditHireDate { get; set; }

    [ObservableProperty]
    public partial Choice EditDepartment { get; set; } = NoDepartment;

    [ObservableProperty]
    public partial Choice EditPosition { get; set; } = NoPosition;

    [ObservableProperty]
    public partial Choice EditManager { get; set; } = NoManager;

    /// <summary>Last day of work entered before "Zakończ zatrudnienie".</summary>
    [ObservableProperty]
    public partial DateTime? TerminationLastDay { get; set; }

    /// <summary>A message after a successful save, so the user sees it worked.</summary>
    [ObservableProperty]
    public partial string? SavedMessage { get; set; }

    public override string Title => Current is { } employee ? $"Kartoteka pracownika – {employee.FirstName} {employee.LastName}" : "Nowy pracownik";

    public bool IsNew => Current is null;

    public bool IsTerminated => Current?.TerminationDate is not null;

    public bool IsEmployed => Current is { TerminationDate: null };

    public string EditorTitle => Current is { } employee ? $"{employee.FirstName} {employee.LastName}" : "Nowy pracownik";

    public string StatusText => Current?.TerminationDate is { } lastDay
        ? $"Zatrudnienie zakończone, ostatni dzień pracy: {lastDay.ToString("d MMMM yyyy", Polish)}."
        : Current is null ? string.Empty : "Zatrudniony.";

    /// <summary>Short status for the badge in the card header.</summary>
    public string StatusBadge => Current is null ? "Nowa kartoteka" : Current.TerminationDate is null ? "Zatrudniony" : "Były pracownik";

    public string CardInitials => Current is { } employee ? Initials.Of(employee.FirstName, employee.LastName) : "+";

    /// <summary>Number, position and department under the name in the card header.</summary>
    public string CardSubtitle => Current is not { } employee
        ? "Uzupełnij dane i kliknij Zapisz. Umowy i urlop dodasz po zapisaniu."
        : string.Join(" · ", new[]
        {
            employee.Number,
            Find(Positions, employee.PositionId)?.Name,
            Find(Departments, employee.DepartmentId)?.Name,
        }.Where(part => !string.IsNullOrEmpty(part)));

    /// <summary>Loads the pickers and the employee (or an empty card when <paramref name="employeeId"/> is null).</summary>
    public Task LoadAsync(Guid? employeeId, CancellationToken cancellationToken) => RunAsync(async () =>
    {
        await LoadChoicesAsync(cancellationToken);
        Show(employeeId is { } id ? await _employees.GetAsync(id, cancellationToken) : null);
    });

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SavedMessage = null;
        if (!Validate())
        {
            return Task.CompletedTask;
        }

        var request = new SaveEmployeeRequest(
            EditNumber,
            EditFirstName,
            EditLastName,
            NullIfEmpty(EditEmail),
            NullIfEmpty(EditPhone),
            DateOnly.FromDateTime(EditHireDate!.Value),
            EditDepartment.Id,
            EditPosition.Id,
            EditManager.Id);

        return RunAsync(async () =>
        {
            var wasNew = IsNew;
            var saved = Current is { } current
                ? await _employees.UpdateAsync(current.Id, request, cancellationToken)
                : await _employees.CreateAsync(request, cancellationToken);

            HasChanges = true;
            Show(saved);
            await RefreshManagersAsync(cancellationToken);
            SavedMessage = wasNew ? "Kartoteka założona. Możesz teraz dodać umowy i urlop." : "Zmiany zapisane.";
        });
    }

    [RelayCommand(CanExecute = nameof(IsEmployed))]
    public Task TerminateAsync(CancellationToken cancellationToken)
    {
        SavedMessage = null;
        if (Current is not { } current)
        {
            return Task.CompletedTask;
        }

        if (TerminationLastDay is not { } lastDay)
        {
            SetError(nameof(TerminationLastDay), "Podaj ostatni dzień pracy.");
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            Show(await _employees.TerminateAsync(current.Id, DateOnly.FromDateTime(lastDay), cancellationToken));
            HasChanges = true;
            await RefreshManagersAsync(cancellationToken);
        });
    }

    [RelayCommand(CanExecute = nameof(IsTerminated))]
    public Task ReinstateAsync(CancellationToken cancellationToken)
    {
        SavedMessage = null;
        return Current is not { } current
            ? Task.CompletedTask
            : RunAsync(async () =>
            {
                Show(await _employees.ReinstateAsync(current.Id, cancellationToken));
                HasChanges = true;
                await RefreshManagersAsync(cancellationToken);
            });
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "employee.number_taken" => nameof(EditNumber),
            "employee.email_taken" => nameof(EditEmail),
            "employee.department_not_found" => nameof(EditDepartment),
            "employee.position_not_found" => nameof(EditPosition),
            "employee.manager_not_found" or "employee.manager_cycle" => nameof(EditManager),
            "employee.hire_after_termination" => nameof(EditHireDate),
            "employee.termination_before_hire" => nameof(TerminationLastDay),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }

    // The contracts and leave tabs follow the card; a new, unsaved card has neither.
    partial void OnCurrentChanged(EmployeeResponse? oldValue, EmployeeResponse? newValue)
    {
        if (oldValue?.Id != newValue?.Id)
        {
            _ = Contracts.ShowAsync(newValue, CancellationToken.None);
            _ = Leave.ShowAsync(newValue, CancellationToken.None);
        }
    }

    private bool Validate()
    {
        SetError(nameof(EditNumber), string.IsNullOrWhiteSpace(EditNumber) ? "Podaj numer ewidencyjny." : null);
        SetError(nameof(EditFirstName), string.IsNullOrWhiteSpace(EditFirstName) ? "Podaj imię." : null);
        SetError(nameof(EditLastName), string.IsNullOrWhiteSpace(EditLastName) ? "Podaj nazwisko." : null);
        SetError(nameof(EditEmail), EmailError(EditEmail));
        SetError(nameof(EditHireDate), EditHireDate is null ? "Podaj datę zatrudnienia." : null);

        return !HasErrors;
    }

    private static string? EmailError(string email)
    {
        var trimmed = email.Trim();
        var at = trimmed.IndexOf('@', StringComparison.Ordinal);

        return trimmed.Length == 0 || (at > 0 && at < trimmed.Length - 1 && !trimmed.Contains(' ', StringComparison.Ordinal))
            ? null
            : "Podaj adres w postaci nazwa@firma.pl albo zostaw puste.";
    }

    private void Show(EmployeeResponse? employee)
    {
        Current = employee;
        OnPropertyChanged(nameof(CardSubtitle));

        EditNumber = employee?.Number ?? string.Empty;
        EditFirstName = employee?.FirstName ?? string.Empty;
        EditLastName = employee?.LastName ?? string.Empty;
        EditEmail = employee?.Email ?? string.Empty;
        EditPhone = employee?.Phone ?? string.Empty;
        EditHireDate = employee is null ? _time.GetLocalNow().Date : employee.HireDate.ToDateTime(TimeOnly.MinValue);
        EditDepartment = Find(Departments, employee?.DepartmentId) ?? NoDepartment;
        EditPosition = Find(Positions, employee?.PositionId) ?? NoPosition;
        RebuildManagerChoices();
        EditManager = Find(Managers, employee?.ManagerId) ?? NoManager;
        TerminationLastDay = employee?.TerminationDate?.ToDateTime(TimeOnly.MinValue) ?? _time.GetLocalNow().Date;

        // Showing a card is not an edit, so it must not leave errors behind.
        ClearAllErrors();
    }

    private async Task LoadChoicesAsync(CancellationToken cancellationToken)
    {
        var departments = await _organization.GetDepartmentsAsync(includeInactive: false, cancellationToken);
        var positions = await _organization.GetPositionsAsync(includeInactive: false, cancellationToken);

        Replace(Departments, NoDepartment, departments.Select(department => new Choice(department.Id, $"{department.Name} ({department.Code})")));
        Replace(Positions, NoPosition, positions.Select(position => new Choice(position.Id, position.Name)));

        await RefreshManagersAsync(cancellationToken);
    }

    private async Task RefreshManagersAsync(CancellationToken cancellationToken)
    {
        _managerCandidates = [.. await _employees.LookupAsync(cancellationToken)];
        var keep = EditManager.Id;
        RebuildManagerChoices();
        EditManager = Find(Managers, keep) ?? NoManager;
    }

    // Nobody can be their own manager, so the open employee is left out of the list.
    private void RebuildManagerChoices() =>
        Replace(Managers, NoManager, _managerCandidates
            .Where(candidate => candidate.Id != Current?.Id)
            .Select(candidate => new Choice(candidate.Id, $"{candidate.FullName} ({candidate.Number})")));

    private static void Replace(ObservableCollection<Choice> target, Choice none, IEnumerable<Choice> choices)
    {
        target.Clear();
        target.Add(none);
        foreach (var choice in choices)
        {
            target.Add(choice);
        }
    }

    private static Choice? Find(IEnumerable<Choice> choices, Guid? id) => id is null ? null : choices.FirstOrDefault(choice => choice.Id == id);

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
