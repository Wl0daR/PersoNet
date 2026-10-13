using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Leave;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>
/// "Urlopy" screen: requests of the whole company (waiting ones first) across the window. A request opens in a dialog with
/// its details and the decision; a new request on behalf of an employee is filled in a dialog too.
/// </summary>
public sealed partial class LeaveViewModel : EditorViewModel
{
    private readonly ILeaveApi _leave;
    private readonly IEmployeesApi _employees;
    private readonly TimeProvider _time;
    private readonly IDialogService _dialogs;

    // Set once constructed, so setting the first year does not load anything yet.
    private readonly bool _isConstructed;

    public LeaveViewModel(ILeaveApi leave, IEmployeesApi employees, TimeProvider time, IDialogService dialogs)
    {
        _leave = leave;
        _employees = employees;
        _time = time;
        _dialogs = dialogs;
        Years = Enumerable.Range(time.GetLocalNow().Year - 1, 3).ToList();
        Year = time.GetLocalNow().Year;
        _isConstructed = true;
    }

    public static IReadOnlyList<LeaveStateChoice> States { get; } =
    [
        new(LeaveState.Pending, "Do rozpatrzenia"),
        new(null, "Wszystkie"),
        new(LeaveState.Approved, "Zatwierdzone"),
        new(LeaveState.Rejected, "Odrzucone"),
        new(LeaveState.Cancelled, "Anulowane"),
    ];

    public ObservableCollection<LeaveRow> Items { get; } = [];

    /// <summary>The current year and its neighbours.</summary>
    public IReadOnlyList<int> Years { get; }

    [ObservableProperty]
    public partial LeaveStateChoice StateFilter { get; set; } = States[0];

    [ObservableProperty]
    public partial int Year { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EmptyTitle), nameof(EmptyHint))]
    public partial bool IsEmpty { get; set; }

    /// <summary>Requests waiting for a decision (in the chosen year), for the menu badge and the start screen.</summary>
    [ObservableProperty]
    public partial int PendingCount { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenCommand))]
    public partial LeaveRow? Selected { get; set; }

    public string EmptyTitle => StateFilter.State == LeaveState.Pending ? "Nie ma wniosków do rozpatrzenia" : "Brak wniosków";

    public string EmptyHint => StateFilter.State == LeaveState.Pending
        ? "Wszystko załatwione. Nowy wniosek dodasz przyciskiem „Nowy wniosek”."
        : "Zmień rok lub filtr albo dodaj nowy wniosek.";

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(() => ReloadAsync(Selected?.Request.Id, cancellationToken));

    /// <summary>Creates the new request dialog with the list of employees; the screenshot tool opens it the same way.</summary>
    public async Task<LeaveFormViewModel> CreateFormAsync(CancellationToken cancellationToken)
    {
        var form = new LeaveFormViewModel(_leave);
        var people = await _employees.LookupAsync(cancellationToken);
        foreach (var person in people)
        {
            form.Employees.Add(new Choice(person.Id, $"{person.FullName} ({person.Number})"));
        }

        var today = _time.GetLocalNow().Date;
        form.Reset(LeaveFormViewModel.NoEmployee, today, today, employeeFixed: false);
        return form;
    }

    /// <summary>Opens the form of a new request on behalf of an employee.</summary>
    [RelayCommand]
    public async Task NewAsync(CancellationToken cancellationToken)
    {
        LeaveFormViewModel? form = null;
        await RunAsync(async () => form = await CreateFormAsync(cancellationToken));
        if (form is null || !await _dialogs.ShowAsync(form) || form.Created is not { } created)
        {
            return;
        }

        // Show the new request even when the filter hides its year or state.
        Year = created.StartDate.Year;
        if (StateFilter.State is { } state && state != created.State)
        {
            StateFilter = States[0];
        }

        await RunAsync(() => ReloadAsync(created.Id, cancellationToken));
    }

    /// <summary>Opens the picked request with its details and decision.</summary>
    [RelayCommand(CanExecute = nameof(CanOpen))]
    public async Task OpenAsync(LeaveRow? row, CancellationToken cancellationToken)
    {
        if ((row ?? Selected) is not { } request)
        {
            return;
        }

        var dialog = new LeaveDecisionViewModel(_leave, request);
        await dialog.LoadAsync(cancellationToken);
        if (await _dialogs.ShowAsync(dialog))
        {
            await RunAsync(() => ReloadAsync(request.Request.Id, cancellationToken));
        }
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    partial void OnStateFilterChanged(LeaveStateChoice value)
    {
        OnPropertyChanged(nameof(EmptyTitle));
        OnPropertyChanged(nameof(EmptyHint));
        _ = RunAsync(() => ReloadAsync(Selected?.Request.Id, CancellationToken.None));
    }

    partial void OnYearChanged(int value)
    {
        if (_isConstructed)
        {
            _ = RunAsync(() => ReloadAsync(Selected?.Request.Id, CancellationToken.None));
        }
    }

    private bool CanOpen(LeaveRow? row) => (row ?? Selected) is not null;

    private async Task ReloadAsync(Guid? keepSelected, CancellationToken cancellationToken)
    {
        var requests = await _leave.ListAsync(StateFilter.State, Year, employeeId: null, cancellationToken);
        var pending = StateFilter.State == LeaveState.Pending
            ? requests.Count
            : requests.Count(request => request.State == LeaveState.Pending);

        Items.Clear();
        foreach (var request in requests)
        {
            Items.Add(LeaveTexts.ToRow(request));
        }

        IsEmpty = Items.Count == 0;
        PendingCount = pending;
        Selected = Items.FirstOrDefault(row => row.Request.Id == keepSelected);
    }
}
