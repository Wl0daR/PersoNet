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
/// "Pracownicy" screen: a searchable, paged list on the left and the selected or new employee's card on the right.
/// The card is hidden until someone is picked or "Dodaj pracownika" is clicked.
/// </summary>
public sealed partial class EmployeesViewModel(IEmployeesApi employees, IOrganizationApi organization, ILeaveApi leave, TimeProvider time) : EditorViewModel
{
    public const int PageSize = 50;
    public const int NumberMaxLength = 20;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 30;

    public static readonly Choice NoDepartment = new(null, "(bez działu)");
    public static readonly Choice NoPosition = new(null, "(bez stanowiska)");
    public static readonly Choice NoManager = new(null, "(bez przełożonego)");

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>Pause after the last key press before the list is searched, so typing does not query on every key.</summary>
    public static readonly TimeSpan SearchDelay = TimeSpan.FromMilliseconds(400);

    private List<EmployeeLookupItem> _managerCandidates = [];
    private CancellationTokenSource? _pendingSearch;

    public ObservableCollection<EmployeeListItem> Items { get; } = [];

    /// <summary>"Umowy" tab of the open card.</summary>
    public ContractsViewModel Contracts { get; } = new(employees, time);

    /// <summary>"Urlop" tab of the open card.</summary>
    public EmployeeLeaveViewModel Leave { get; } = new(leave, time);

    public ObservableCollection<Choice> Departments { get; } = [NoDepartment];

    public ObservableCollection<Choice> Positions { get; } = [NoPosition];

    public ObservableCollection<Choice> Managers { get; } = [NoManager];

    [ObservableProperty]
    public partial string Search { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IncludeFormer { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    public partial int Page { get; set; } = 1;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo))]
    [NotifyCanExecuteChangedFor(nameof(PreviousPageCommand), nameof(NextPageCommand))]
    public partial int TotalPages { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PageInfo))]
    public partial int TotalCount { get; set; }

    /// <summary>True when the list is empty, so the view can explain why (no employees yet, or nothing matches).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyTitle), nameof(EmptyHint))]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial EmployeeListItem? Selected { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew), nameof(EditorTitle), nameof(IsTerminated), nameof(IsEmployed), nameof(StatusText), nameof(StatusBadge), nameof(CardInitials), nameof(CardSubtitle))]
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

    /// <summary>True while a card (existing or new) is shown next to the list.</summary>
    [ObservableProperty]
    public partial bool IsCardOpen { get; set; }

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
        ? "Uzupełnij dane i kliknij Zapisz."
        : string.Join(" · ", new[]
        {
            employee.Number,
            Find(Positions, employee.PositionId)?.Name,
            Find(Departments, employee.DepartmentId)?.Name,
        }.Where(part => !string.IsNullOrEmpty(part)));

    public string PageInfo => TotalCount == 0
        ? string.Empty
        : $"Strona {Page} z {Math.Max(TotalPages, 1)} · {Pluralize(TotalCount)}";

    public string EmptyTitle => string.IsNullOrWhiteSpace(Search) && !IncludeFormer ? "Nie ma jeszcze pracowników" : "Nikt nie pasuje do wyszukiwania";

    public string EmptyHint => string.IsNullOrWhiteSpace(Search) && !IncludeFormer
        ? "Kliknij „Dodaj pracownika”, aby założyć pierwszą kartotekę."
        : "Sprawdź pisownię albo zaznacz „Pokaż byłych pracowników”.";

    /// <summary>Closes the card and goes back to the list alone.</summary>
    [RelayCommand]
    public void CloseCard()
    {
        Selected = null;
        Show(null);
        IsCardOpen = false;
    }

    /// <summary>Loads the pickers (departments, positions, managers) and the first page of the list.</summary>
    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        await LoadChoicesAsync(cancellationToken);
        await LoadPageAsync(Page, Current?.Id, cancellationToken);
    });

    /// <summary>Runs the search from the first page.</summary>
    [RelayCommand]
    public Task SearchAsync(CancellationToken cancellationToken) => RunAsync(() => LoadPageAsync(1, Current?.Id, cancellationToken));

    [RelayCommand(CanExecute = nameof(HasPreviousPage))]
    public Task PreviousPageAsync(CancellationToken cancellationToken) => RunAsync(() => LoadPageAsync(Page - 1, Current?.Id, cancellationToken));

    [RelayCommand(CanExecute = nameof(HasNextPage))]
    public Task NextPageAsync(CancellationToken cancellationToken) => RunAsync(() => LoadPageAsync(Page + 1, Current?.Id, cancellationToken));

    /// <summary>Opens the card of an employee picked in the list.</summary>
    [RelayCommand]
    public Task OpenAsync(EmployeeListItem? item, CancellationToken cancellationToken)
    {
        if (item is null || item.Id == Current?.Id)
        {
            return Task.CompletedTask;
        }

        return RunAsync(async () => Show(await employees.GetAsync(item.Id, cancellationToken)));
    }

    /// <summary>Clears the card for a new employee.</summary>
    [RelayCommand]
    public void New()
    {
        Selected = null;
        Show(null);
    }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
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
            var saved = Current is { } current
                ? await employees.UpdateAsync(current.Id, request, cancellationToken)
                : await employees.CreateAsync(request, cancellationToken);

            Show(saved);
            await RefreshManagersAsync(cancellationToken);
            await LoadPageAsync(Page, saved.Id, cancellationToken);
        });
    }

    [RelayCommand(CanExecute = nameof(IsEmployed))]
    public Task TerminateAsync(CancellationToken cancellationToken)
    {
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
            Show(await employees.TerminateAsync(current.Id, DateOnly.FromDateTime(lastDay), cancellationToken));
            await RefreshManagersAsync(cancellationToken);
            await LoadPageAsync(Page, current.Id, cancellationToken);
        });
    }

    [RelayCommand(CanExecute = nameof(IsTerminated))]
    public Task ReinstateAsync(CancellationToken cancellationToken) => Current is not { } current
        ? Task.CompletedTask
        : RunAsync(async () =>
        {
            Show(await employees.ReinstateAsync(current.Id, cancellationToken));
            await RefreshManagersAsync(cancellationToken);
            await LoadPageAsync(Page, current.Id, cancellationToken);
        });

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

    // The contracts tab follows the open card; a new, unsaved card has no contracts yet.
    partial void OnCurrentChanged(EmployeeResponse? oldValue, EmployeeResponse? newValue)
    {
        if (oldValue?.Id != newValue?.Id)
        {
            _ = Contracts.ShowAsync(newValue, CancellationToken.None);
            _ = Leave.ShowAsync(newValue, CancellationToken.None);
        }
    }

    partial void OnIncludeFormerChanged(bool value) => _ = SearchAsync(CancellationToken.None);

    // Search as you type: each key press restarts the wait, and only the last one runs the search.
    partial void OnSearchChanged(string value)
    {
        _pendingSearch?.Cancel();
        _pendingSearch?.Dispose();
        _pendingSearch = new CancellationTokenSource();
        _ = SearchAfterPauseAsync(_pendingSearch.Token);
    }

    private async Task SearchAfterPauseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(SearchDelay, time, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        await SearchAsync(cancellationToken);
    }

    private bool HasPreviousPage() => Page > 1;

    private bool HasNextPage() => Page < TotalPages;

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
        IsCardOpen = true;
        ClearAllErrors();

        EditNumber = employee?.Number ?? string.Empty;
        EditFirstName = employee?.FirstName ?? string.Empty;
        EditLastName = employee?.LastName ?? string.Empty;
        EditEmail = employee?.Email ?? string.Empty;
        EditPhone = employee?.Phone ?? string.Empty;
        EditHireDate = employee is null ? time.GetLocalNow().Date : employee.HireDate.ToDateTime(TimeOnly.MinValue);
        EditDepartment = Find(Departments, employee?.DepartmentId) ?? NoDepartment;
        EditPosition = Find(Positions, employee?.PositionId) ?? NoPosition;
        RebuildManagerChoices();
        EditManager = Find(Managers, employee?.ManagerId) ?? NoManager;
        TerminationLastDay = employee?.TerminationDate?.ToDateTime(TimeOnly.MinValue) ?? time.GetLocalNow().Date;

        // Showing a card is not an edit, so it must not leave errors behind.
        ClearAllErrors();
    }

    private async Task LoadPageAsync(int page, Guid? keepSelected, CancellationToken cancellationToken)
    {
        var result = await employees.ListAsync(new EmployeeListQuery(Search, null, IncludeFormer, Math.Max(page, 1), PageSize), cancellationToken);

        // Asking past the last page (e.g. after a filter shrank the list) falls back to the last one.
        if (result.Items.Count == 0 && result.TotalCount > 0 && page > 1)
        {
            result = await employees.ListAsync(new EmployeeListQuery(Search, null, IncludeFormer, result.TotalPages, PageSize), cancellationToken);
        }

        Items.Clear();
        foreach (var item in result.Items)
        {
            Items.Add(item);
        }

        Page = result.Page;
        TotalPages = result.TotalPages;
        TotalCount = result.TotalCount;
        IsEmpty = Items.Count == 0;
        Selected = Items.FirstOrDefault(item => item.Id == keepSelected);
    }

    private async Task LoadChoicesAsync(CancellationToken cancellationToken)
    {
        var departments = await organization.GetDepartmentsAsync(includeInactive: false, cancellationToken);
        var positions = await organization.GetPositionsAsync(includeInactive: false, cancellationToken);

        Replace(Departments, NoDepartment, departments.Select(department => new Choice(department.Id, $"{department.Name} ({department.Code})")));
        Replace(Positions, NoPosition, positions.Select(position => new Choice(position.Id, position.Name)));

        await RefreshManagersAsync(cancellationToken);
    }

    private async Task RefreshManagersAsync(CancellationToken cancellationToken)
    {
        _managerCandidates = [.. await employees.LookupAsync(cancellationToken)];
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

    private static string Pluralize(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;

        var word = count == 1 ? "osoba"
            : last is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? "osoby"
            : "osób";

        return $"{count} {word}";
    }
}
