using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Leave;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>
/// Form of a new leave request: who, what kind, which days (or hours of one day) and a note. Used on the "Urlopy" screen,
/// where HR picks the employee, and in the calendar of an employee's card, where the employee is fixed.
/// </summary>
public sealed partial class LeaveFormViewModel(ILeaveApi leave) : EditorViewModel
{
    public const int NoteMaxLength = 500;

    public static readonly Choice NoEmployee = new(null, "(wybierz pracownika)");

    public ObservableCollection<Choice> Employees { get; } = [NoEmployee];

    /// <summary>True in an employee's card: the request is for that employee and the picker is hidden.</summary>
    [ObservableProperty]
    public partial bool IsEmployeeFixed { get; set; }

    [ObservableProperty]
    public partial Choice EditEmployee { get; set; } = NoEmployee;

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
    public async Task<LeaveRequestResponse?> SubmitAsync(CancellationToken cancellationToken)
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
    }

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
