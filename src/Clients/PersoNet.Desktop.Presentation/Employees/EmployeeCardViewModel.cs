using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Absences;
using PersoNet.ApiClient.Access;
using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Leave;
using PersoNet.ApiClient.Organization;
using PersoNet.ApiClient.Safety;
using PersoNet.ApiClient.WorkTime;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Absences;
using PersoNet.Desktop.Presentation.Access;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Leave;
using PersoNet.Desktop.Presentation.Safety;
using PersoNet.Desktop.Presentation.WorkTime;

namespace PersoNet.Desktop.Presentation.Employees;

/// <summary>
/// Employee card, in a window of its own: the personal and employment data ("Dane"), the contracts, the leave and the
/// working time.
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
    private IReadOnlyList<PositionResponse> _allPositions = [];
    private Dictionary<Guid, Guid?> _parentOf = [];

    public EmployeeCardViewModel(
        IEmployeesApi employees,
        IOrganizationApi organization,
        ILeaveApi leave,
        IAbsencesApi absences,
        IAccessApi access,
        IWorkTimeApi workTime,
        ISafetyApi safety,
        IFileService files,
        UserAccess user,
        TimeProvider time,
        IDialogService dialogs)
    {
        ArgumentNullException.ThrowIfNull(user);

        _employees = employees;
        _organization = organization;
        _time = time;
        CanEdit = user.Can(UserAccess.EmployeesManage);
        CanManageAccount = user.Can(UserAccess.AccountsManage);
        CanManageRoles = user.Can(UserAccess.RolesManage);
        CanSeeHistory = user.Can(UserAccess.AuditRead);
        CanSeeLeave = user.Can(UserAccess.LeaveRead);
        CanSeeWorkTime = user.Can(UserAccess.WorkTimeRead);
        CanSeeSafety = user.Can(UserAccess.SafetyRead);
        Contracts = new ContractsViewModel(employees, time, dialogs);
        Leave = new EmployeeLeaveViewModel(leave, time, dialogs);
        Absences = new EmployeeAbsencesViewModel(absences, time, dialogs, user.Can(UserAccess.LeaveManage));
        WorkTime = new EmployeeWorkTimeViewModel(workTime, files, time, dialogs, user.Can(UserAccess.WorkTimeManage));
        Safety = new EmployeeSafetyViewModel(safety, time, dialogs, user.Can(UserAccess.SafetyManage));
        Account = new EmployeeAccountViewModel(employees);
        Roles = new EmployeeRolesViewModel(access);
        History = new EmployeeHistoryViewModel(employees);
    }

    /// <summary>False for someone who may only look at the card: the "Dane" tab is read-only and there is no "Zapisz".</summary>
    public bool CanEdit { get; }

    /// <summary>The "Konto" tab shows the portal account to someone who manages accounts.</summary>
    public bool CanManageAccount { get; }

    /// <summary>The "Konto" tab shows the roles to someone who manages roles.</summary>
    public bool CanManageRoles { get; }

    /// <summary>The "Konto" tab is there when it has something the user may manage.</summary>
    public bool CanSeeAccessTab => CanManageAccount || CanManageRoles;

    public bool CanSeeHistory { get; }

    public bool CanSeeLeave { get; }

    public bool CanSeeWorkTime { get; }

    public bool CanSeeSafety { get; }

    /// <summary>Roles of the employee, in the "Konto" tab.</summary>
    public EmployeeRolesViewModel Roles { get; }

    /// <summary>"Umowy" tab.</summary>
    public ContractsViewModel Contracts { get; }

    /// <summary>"Urlop" tab.</summary>
    public EmployeeLeaveViewModel Leave { get; }

    /// <summary>"Nieobecności" tab: sickness, business trips, remote work.</summary>
    public EmployeeAbsencesViewModel Absences { get; }

    /// <summary>"Czas pracy" tab.</summary>
    public EmployeeWorkTimeViewModel WorkTime { get; }

    /// <summary>"Badania i szkolenia" tab.</summary>
    public EmployeeSafetyViewModel Safety { get; }

    /// <summary>"Konto" tab: access to the employee portal.</summary>
    public EmployeeAccountViewModel Account { get; }

    /// <summary>"Historia" tab: who changed what and when.</summary>
    public EmployeeHistoryViewModel History { get; }

    public ObservableCollection<Choice> Departments { get; } = [NoDepartment];

    public ObservableCollection<Choice> Positions { get; } = [NoPosition];

    public ObservableCollection<Choice> Managers { get; } = [NoManager];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew), nameof(Title), nameof(EditorTitle), nameof(IsTerminated), nameof(IsEmployed), nameof(StatusText), nameof(StatusBadge), nameof(CardInitials), nameof(CardSubtitle))]
    [NotifyCanExecuteChangedFor(nameof(TerminateCommand), nameof(ReinstateCommand), nameof(RehireCommand))]
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
    [NotifyPropertyChangedFor(nameof(CanFilterPositions))]
    public partial Choice EditDepartment { get; set; } = NoDepartment;

    /// <summary>
    /// Off: the position list has only the chosen department's positions (also those of the departments above it) and
    /// the ones tied to no department. On: every position.
    /// </summary>
    [ObservableProperty]
    public partial bool ShowAllPositions { get; set; }

    /// <summary>True when a department is chosen, so the position list can be narrowed to it.</summary>
    public bool CanFilterPositions => EditDepartment.Id is not null;

    [ObservableProperty]
    public partial Choice EditPosition { get; set; } = NoPosition;

    [ObservableProperty]
    public partial Choice EditManager { get; set; } = NoManager;

    /// <summary>Last day of work entered before "Zakończ zatrudnienie".</summary>
    [ObservableProperty]
    public partial DateTime? TerminationLastDay { get; set; }

    /// <summary>First day of the new employment entered before "Zatrudnij ponownie".</summary>
    [ObservableProperty]
    public partial DateTime? RehireDate { get; set; }

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
            _allPositions.FirstOrDefault(position => position.Id == employee.PositionId)?.Name,
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

    /// <summary>"Zatrudnij ponownie": a former employee returns from the given day; earlier contracts stay ended.</summary>
    [RelayCommand(CanExecute = nameof(IsTerminated))]
    public Task RehireAsync(CancellationToken cancellationToken)
    {
        SavedMessage = null;
        if (Current is not { } current)
        {
            return Task.CompletedTask;
        }

        if (RehireDate is not { } hireDate)
        {
            SetError(nameof(RehireDate), "Podaj pierwszy dzień nowego zatrudnienia.");
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            Show(await _employees.RehireAsync(current.Id, DateOnly.FromDateTime(hireDate), cancellationToken));
            HasChanges = true;
            await RefreshManagersAsync(cancellationToken);
            SavedMessage = "Pracownik zatrudniony ponownie. Dodaj nową umowę w zakładce Umowy.";
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
            "employee.rehire_before_termination" or "employee.not_terminated" => nameof(RehireDate),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }

    // The contracts, leave, working time, account and history tabs follow the card; a new, unsaved card has none of them. Ending or resuming the employment
    // changes the contracts too.
    partial void OnCurrentChanged(EmployeeResponse? oldValue, EmployeeResponse? newValue)
    {
        if (oldValue?.Id != newValue?.Id || oldValue?.TerminationDate != newValue?.TerminationDate || oldValue?.HireDate != newValue?.HireDate)
        {
            _ = Contracts.ShowAsync(newValue, CancellationToken.None);
            if (CanSeeLeave)
            {
                _ = Leave.ShowAsync(newValue, CancellationToken.None);
                _ = Absences.ShowAsync(newValue, CancellationToken.None);
            }

            if (CanSeeWorkTime)
            {
                _ = WorkTime.ShowAsync(newValue, CancellationToken.None);
            }

            if (CanSeeSafety)
            {
                _ = Safety.ShowAsync(newValue, CancellationToken.None);
            }
        }

        // The account is found by the e-mail, so a changed e-mail may mean another account.
        if (oldValue?.Id != newValue?.Id || oldValue?.Email != newValue?.Email)
        {
            if (CanManageAccount)
            {
                _ = Account.ShowAsync(newValue, CancellationToken.None);
            }
        }

        if (CanManageRoles && oldValue?.Id != newValue?.Id)
        {
            _ = Roles.ShowAsync(newValue, CancellationToken.None);
        }

        // Any saved change adds to the history.
        if (CanSeeHistory)
        {
            _ = History.ShowAsync(newValue, CancellationToken.None);
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
        ShowAllPositions = false;
        EditDepartment = Find(Departments, employee?.DepartmentId) ?? NoDepartment;
        RebuildPositionChoices(employee?.PositionId);
        RebuildManagerChoices();
        EditManager = Find(Managers, employee?.ManagerId) ?? NoManager;
        TerminationLastDay = employee?.TerminationDate?.ToDateTime(TimeOnly.MinValue) ?? _time.GetLocalNow().Date;

        // A returning employee usually starts today, and never before the day after they left.
        var today = DateOnly.FromDateTime(_time.GetLocalNow().Date);
        RehireDate = employee?.TerminationDate is { } left
            ? (today > left ? today : left.AddDays(1)).ToDateTime(TimeOnly.MinValue)
            : null;

        // Showing a card is not an edit, so it must not leave errors behind.
        ClearAllErrors();
    }

    private async Task LoadChoicesAsync(CancellationToken cancellationToken)
    {
        var departments = await _organization.GetDepartmentsAsync(includeInactive: false, cancellationToken);
        var positions = await _organization.GetPositionsAsync(includeInactive: false, cancellationToken);

        Replace(Departments, NoDepartment, departments.Select(department => new Choice(department.Id, $"{department.Name} ({department.Code})")));
        _allPositions = positions;
        _parentOf = departments.ToDictionary(department => department.Id, department => department.ParentId);
        RebuildPositionChoices(EditPosition.Id);

        await RefreshManagersAsync(cancellationToken);
    }

    private async Task RefreshManagersAsync(CancellationToken cancellationToken)
    {
        _managerCandidates = [.. await _employees.LookupAsync(cancellationToken)];
        var keep = EditManager.Id;
        RebuildManagerChoices();
        EditManager = Find(Managers, keep) ?? NoManager;
    }

    partial void OnEditDepartmentChanged(Choice value) => RebuildPositionChoices(EditPosition.Id);

    partial void OnShowAllPositionsChanged(bool value) => RebuildPositionChoices(EditPosition.Id);

    // The chosen position always stays in the list, even when it does not belong to the department, so nothing is lost.
    private void RebuildPositionChoices(Guid? keep)
    {
        // The chosen department and those above it; a position of "Produkcja" fits its "Lakiernia" too.
        var departments = new HashSet<Guid>();
        var id = EditDepartment.Id;
        while (id is { } current && departments.Add(current))
        {
            id = _parentOf.GetValueOrDefault(current);
        }

        var filter = !ShowAllPositions && departments.Count > 0;
        Replace(Positions, NoPosition, _allPositions
            .Where(position => !filter || position.Id == keep || position.DepartmentIds.Count == 0 || position.DepartmentIds.Any(departments.Contains))
            .Select(position => new Choice(position.Id, position.Name)));
        EditPosition = Find(Positions, keep) ?? NoPosition;
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
