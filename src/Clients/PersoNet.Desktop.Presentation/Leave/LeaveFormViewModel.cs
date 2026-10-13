using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Leave;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>
/// Dialog with the form of a new leave request: who, what kind, which days (or hours of one day) and a note, with the
/// employee's annual leave balance next to it. Opened from the "Urlopy" screen, where HR picks the employee, and from the
/// calendar of an employee's card, where the employee is fixed.
/// </summary>
public sealed partial class LeaveFormViewModel(ILeaveApi leave) : DialogViewModel
{
    public const int NoteMaxLength = 500;

    public static readonly Choice NoEmployee = new(null, "(wybierz pracownika)");

    public ObservableCollection<Choice> Employees { get; } = [NoEmployee];

    private int _balanceVersion;

    /// <summary>True in an employee's card: the request is for that employee and the picker is hidden.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial bool IsEmployeeFixed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Title))]
    public partial Choice EditEmployee { get; set; } = NoEmployee;

    /// <summary>Annual leave balance of the picked employee in the year of the first day.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BalanceText))]
    public partial LeaveBalanceResponse? Balance { get; set; }

    /// <summary>The request after it was sent.</summary>
    public LeaveRequestResponse? Created { get; private set; }

    public override string Title => IsEmployeeFixed ? $"Nowy wniosek urlopowy – {EditEmployee.Name}" : "Nowy wniosek urlopowy";

    public string? BalanceText => LeaveTexts.BalanceText(Balance);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanUseHours), nameof(PeriodHint))]
    public partial LeaveKindChoice EditKind { get; set; } = LeaveTexts.Kinds[0];

    /// <summary>Leave taken in hours on one day instead of whole days.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StartLabel), nameof(PeriodHint))]
    public partial bool IsHourly { get; set; }

    /// <summary>Hours as typed: "2", "2,5" or "2:30".</summary>
    [ObservableProperty]
    public partial string EditHours { get; set; } = string.Empty;

    [ObservableProperty]
    public partial DateTime? EditStartDate { get; set; }

    [ObservableProperty]
    public partial DateTime? EditEndDate { get; set; }

    [ObservableProperty]
    public partial string EditNote { get; set; } = string.Empty;

    /// <summary>Child care, force majeure and the rest of annual leave can be taken in hours (Labour Code).</summary>
    public bool CanUseHours => LeaveTexts.AllowsHours(EditKind.Kind);

    public string StartLabel => IsHourly ? "_Dzień *" : "_Od *";

    public string PeriodHint => !IsHourly
        ? "Liczą się dni robocze: bez weekendów i świąt."
        : EditKind.Kind == LeaveKind.Annual
            ? "Np. 2, 2,5 albo 2:30. Urlop wypoczynkowy na godziny tylko na resztę urlopu mniejszą niż dzień pracy."
            : "Np. 2, 2,5 albo 2:30. W roku 16 godzin (przy niepełnym etacie proporcjonalnie mniej).";

    /// <summary>Starts an empty form for <paramref name="employee"/> (or for a pick) on the given days.</summary>
    public void Reset(Choice employee, DateTime start, DateTime end, bool employeeFixed)
    {
        ArgumentNullException.ThrowIfNull(employee);

        IsEmployeeFixed = employeeFixed;
        EditEmployee = employee;
        EditKind = LeaveTexts.Kinds[0];
        IsHourly = false;
        EditHours = string.Empty;
        EditStartDate = start;
        EditEndDate = end < start ? start : end;
        EditNote = string.Empty;
        ClearAllErrors();
    }

    /// <summary>Sends the request; returns it, or null when a field or the server refused it (the error is shown).</summary>
    public async Task<LeaveRequestResponse?> SendAsync(CancellationToken cancellationToken)
    {
        if (!Validate())
        {
            return null;
        }

        var request = new CreateLeaveRequest(
            EditEmployee.Id!.Value,
            EditKind.Kind,
            DateOnly.FromDateTime(EditStartDate!.Value),
            DateOnly.FromDateTime(IsHourly ? EditStartDate!.Value : EditEndDate!.Value),
            string.IsNullOrWhiteSpace(EditNote) ? null : EditNote.Trim(),
            IsHourly ? LeaveTexts.ParseHours(EditHours) : null);

        LeaveRequestResponse? created = null;
        await RunAsync(async () => created = await leave.CreateAsync(request, cancellationToken));
        return created;
    }

    /// <summary>"Złóż wniosek": sends the request and closes the dialog when the server accepts it.</summary>
    [RelayCommand]
    public async Task SubmitAsync(CancellationToken cancellationToken)
    {
        if (await SendAsync(cancellationToken) is { } created)
        {
            Created = created;
            HasChanges = true;
            Close(true);
        }
    }

    /// <summary>Loads the balance shown next to the form, for the picked employee and the year of the first day.</summary>
    public async Task LoadBalanceAsync(CancellationToken cancellationToken)
    {
        var version = ++_balanceVersion;
        if (EditEmployee.Id is not { } id || EditStartDate is not { } start)
        {
            Balance = null;
            return;
        }

        LeaveBalanceResponse? balance;
        try
        {
            balance = await leave.GetBalanceAsync(id, start.Year, cancellationToken);
        }
        catch (ApiException)
        {
            balance = null;
        }
        catch (HttpRequestException)
        {
            balance = null;
        }

        // Only the answer for the latest pick is shown.
        if (version == _balanceVersion)
        {
            Balance = balance;
        }
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "leave.employee_not_found" => nameof(EditEmployee),
            "leave.no_entitlement" or "leave.on_demand_limit" or "leave.child_care_limit" or "leave.force_majeure_limit" => nameof(EditKind),
            "leave.not_enough_days" => IsHourly ? nameof(EditHours) : nameof(EditKind),
            "leave.hours_invalid" or "leave.hours_only_for_remainder" or "leave.hours_not_allowed" or "leave.hours_one_day" => nameof(EditHours),
            "leave.days_or_hours" => IsHourly ? nameof(EditHours) : nameof(EditKind),
            "leave.outside_employment" or "leave.overlap" => nameof(EditStartDate),
            "leave.end_before_start" or "leave.crosses_year" or "leave.no_working_days" => IsHourly ? nameof(EditStartDate) : nameof(EditEndDate),
            "leave.note_too_long" => nameof(EditNote),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }

    // A one-day leave is the most common case, so the end follows the start until it is set later than it.
    // Leave in hours is always one day.
    partial void OnEditStartDateChanged(DateTime? value)
    {
        if (value is { } start && (IsHourly || EditEndDate is null || EditEndDate < start))
        {
            EditEndDate = start;
        }

        if (value?.Year != Balance?.Year)
        {
            _ = LoadBalanceAsync(CancellationToken.None);
        }
    }

    partial void OnEditEmployeeChanged(Choice value) => _ = LoadBalanceAsync(CancellationToken.None);

    partial void OnEditKindChanged(LeaveKindChoice value)
    {
        if (!LeaveTexts.AllowsHours(value.Kind))
        {
            IsHourly = false;
        }
    }

    partial void OnIsHourlyChanged(bool value)
    {
        SetError(nameof(EditHours), null);
        if (value)
        {
            EditEndDate = EditStartDate;
        }
    }

    private bool Validate()
    {
        SetError(nameof(EditEmployee), EditEmployee.Id is null ? "Wybierz pracownika." : null);
        SetError(nameof(EditStartDate), EditStartDate is null ? "Podaj pierwszy dzień urlopu." : null);
        SetError(nameof(EditEndDate), IsHourly ? null : EditEndDate is null ? "Podaj ostatni dzień urlopu."
            : EditStartDate is { } start && EditEndDate < start ? "Ostatni dzień nie może być przed pierwszym." : null);
        SetError(nameof(EditHours), IsHourly && LeaveTexts.ParseHours(EditHours) is not > 0 ? "Podaj liczbę godzin, np. 2, 2,5 albo 2:30." : null);
        SetError(nameof(EditNote), EditNote.Trim().Length > NoteMaxLength ? $"Uwagi mogą mieć najwyżej {NoteMaxLength} znaków." : null);

        return !HasErrors;
    }
}
