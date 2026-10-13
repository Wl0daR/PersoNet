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
/// "Urlopy" screen: requests of the whole company (waiting ones first) on the left; on the right the picked request with
/// its decision, or the form of a new request registered on behalf of an employee.
/// </summary>
public sealed partial class LeaveViewModel : EditorViewModel
{
    public const int NoteMaxLength = LeaveFormViewModel.NoteMaxLength;

    private readonly ILeaveApi _leave;
    private readonly IEmployeesApi _employees;
    private readonly TimeProvider _time;

    // Set once constructed, so setting the first year does not load anything yet.
    private readonly bool _isConstructed;

    public LeaveViewModel(ILeaveApi leave, IEmployeesApi employees, TimeProvider time)
    {
        _leave = leave;
        _employees = employees;
        _time = time;
        Years = Enumerable.Range(time.GetLocalNow().Year - 1, 3).ToList();
        Year = time.GetLocalNow().Year;
        Form = new LeaveFormViewModel(leave);

        // The balance next to the form follows the employee and the year picked in it.
        Form.PropertyChanged += (_, e) =>
        {
            if (IsCreating && e.PropertyName is nameof(LeaveFormViewModel.EditEmployee) or nameof(LeaveFormViewModel.EditStartDate))
            {
                ShowFormBalance();
            }
        };

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

    /// <summary>The new request form (on behalf of an employee picked in it).</summary>
    public LeaveFormViewModel Form { get; }

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
    [NotifyPropertyChangedFor(nameof(IsShowingRequest), nameof(IsPane))]
    [NotifyCanExecuteChangedFor(nameof(ApproveCommand), nameof(RejectCommand), nameof(CancelLeaveCommand))]
    public partial LeaveRow? Selected { get; set; }

    /// <summary>True while the new request form is shown instead of a picked request.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShowingRequest), nameof(IsPane))]
    public partial bool IsCreating { get; set; }

    [ObservableProperty]
    public partial LeaveBalanceResponse? Balance { get; set; }

    /// <summary>Note with a decision; the reason when rejecting.</summary>
    [ObservableProperty]
    public partial string DecisionNote { get; set; } = string.Empty;

    /// <summary>First click on "Anuluj urlop" asks for confirmation; the second one cancels.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelText))]
    public partial bool IsConfirmingCancel { get; set; }

    public bool IsShowingRequest => Selected is not null && !IsCreating;

    /// <summary>True when the right pane has something to show (a request or the form).</summary>
    public bool IsPane => IsShowingRequest || IsCreating;

    public string CancelText => IsConfirmingCancel ? "Na pewno anulować?" : "Anuluj urlop";

    public string EmptyTitle => StateFilter.State == LeaveState.Pending ? "Nie ma wniosków do rozpatrzenia" : "Brak wniosków";

    public string EmptyHint => StateFilter.State == LeaveState.Pending
        ? "Wszystko załatwione. Nowy wniosek dodasz przyciskiem „Nowy wniosek”."
        : "Zmień rok lub filtr albo dodaj nowy wniosek.";

    /// <summary>Text of the annual leave balance of the employee in the pane, e.g. "Zostało 14 z 26 dni (2 czekają na decyzję)".</summary>
    public string? BalanceText => Balance switch
    {
        null => null,
        { HasEntitlement: false } => $"Wymiar urlopu na {Balance.Year} nie jest ustawiony (karta pracownika, zakładka Urlop).",
        _ => $"Urlop wypoczynkowy {Balance.Year}: zostało {LeaveTexts.Amount(Balance.RemainingHours)} z {LeaveTexts.Amount(Balance.TotalHours)}"
            + (Balance.PendingHours > 0 ? $" ({LeaveTexts.Amount(Balance.PendingHours)} czeka na decyzję)" : string.Empty) + "."
            + (Balance.DailyHours != LeaveTexts.HoursPerDay ? $" Dzień urlopu tego pracownika to {LeaveTexts.Hours(Balance.DailyHours)}" : string.Empty),
    };

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var people = await _employees.LookupAsync(cancellationToken);
        var keep = Form.EditEmployee.Id;
        Form.Employees.Clear();
        Form.Employees.Add(LeaveFormViewModel.NoEmployee);
        foreach (var person in people)
        {
            Form.Employees.Add(new Choice(person.Id, $"{person.FullName} ({person.Number})"));
        }

        Form.EditEmployee = Form.Employees.FirstOrDefault(choice => choice.Id == keep) ?? LeaveFormViewModel.NoEmployee;
        await ReloadAsync(Selected?.Request.Id, cancellationToken);
    });

    /// <summary>Opens the empty form of a new request.</summary>
    [RelayCommand]
    public void New()
    {
        Selected = null;
        IsCreating = true;
        Balance = null;
        var today = _time.GetLocalNow().Date;
        Form.Reset(LeaveFormViewModel.NoEmployee, today, today, employeeFixed: false);
        ClearAllErrors();
    }

    [RelayCommand]
    public void ClosePane()
    {
        IsCreating = false;
        Selected = null;
        ClearAllErrors();
    }

    [RelayCommand]
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (await Form.SubmitAsync(cancellationToken) is not { } created)
        {
            return;
        }

        IsCreating = false;

        // Show the new request even when the filter hides its year or state.
        Year = created.StartDate.Year;
        if (StateFilter.State is { } state && state != created.State)
        {
            StateFilter = States[0];
        }

        await RunAsync(() => ReloadAsync(created.Id, cancellationToken));
    }

    [RelayCommand(CanExecute = nameof(CanDecide))]
    public Task ApproveAsync(CancellationToken cancellationToken) => Selected is not { } row
        ? Task.CompletedTask
        : RunAsync(async () => await AfterDecisionAsync(await _leave.ApproveAsync(row.Request.Id, NoteOrNull(), cancellationToken), cancellationToken));

    [RelayCommand(CanExecute = nameof(CanDecide))]
    public Task RejectAsync(CancellationToken cancellationToken)
    {
        if (Selected is not { } row)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(DecisionNote))
        {
            SetError(nameof(DecisionNote), "Podaj powód odrzucenia, żeby pracownik wiedział, co zmienić.");
            return Task.CompletedTask;
        }

        return RunAsync(async () => await AfterDecisionAsync(await _leave.RejectAsync(row.Request.Id, DecisionNote.Trim(), cancellationToken), cancellationToken));
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    public Task CancelLeaveAsync(CancellationToken cancellationToken)
    {
        if (Selected is not { } row)
        {
            return Task.CompletedTask;
        }

        if (!IsConfirmingCancel)
        {
            IsConfirmingCancel = true;
            return Task.CompletedTask;
        }

        return RunAsync(async () => await AfterDecisionAsync(await _leave.CancelAsync(row.Request.Id, NoteOrNull(), cancellationToken), cancellationToken));
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "leave.note_too_long" or "leave.reason_required" => nameof(DecisionNote),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }

    partial void OnSelectedChanged(LeaveRow? value)
    {
        IsConfirmingCancel = false;
        DecisionNote = string.Empty;
        ClearAllErrors();

        if (value is not null)
        {
            IsCreating = false;
            _ = LoadBalanceAsync(value.Request.EmployeeId, value.Request.StartDate.Year);
        }
    }

    private void ShowFormBalance()
    {
        if (Form.EditEmployee.Id is { } id)
        {
            _ = LoadBalanceAsync(id, (Form.EditStartDate ?? _time.GetLocalNow().Date).Year);
        }
        else
        {
            Balance = null;
        }
    }

    partial void OnBalanceChanged(LeaveBalanceResponse? value) => OnPropertyChanged(nameof(BalanceText));

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

    private bool CanDecide() => Selected?.IsPending == true;

    private bool CanCancel() => Selected is { IsPending: true } or { IsApproved: true };

    private string? NoteOrNull() => string.IsNullOrWhiteSpace(DecisionNote) ? null : DecisionNote.Trim();

    private async Task AfterDecisionAsync(LeaveRequestResponse decided, CancellationToken cancellationToken)
    {
        await ReloadAsync(decided.Id, cancellationToken);

        // A decided request may drop out of the "to decide" list; keep it on screen so the result is visible.
        if (Selected is null)
        {
            Selected = LeaveTexts.ToRow(decided);
        }
    }

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

        if (!IsCreating)
        {
            Selected = Items.FirstOrDefault(row => row.Request.Id == keepSelected);
        }
    }

    private async Task LoadBalanceAsync(Guid employeeId, int year)
    {
        try
        {
            var balance = await _leave.GetBalanceAsync(employeeId, year, CancellationToken.None);

            // Only if the pane still shows the same person.
            var current = IsCreating ? Form.EditEmployee.Id : Selected?.Request.EmployeeId;
            if (current == employeeId)
            {
                Balance = balance;
            }
        }
        catch (ApiException)
        {
            Balance = null;
        }
        catch (HttpRequestException)
        {
            Balance = null;
        }
    }
}
