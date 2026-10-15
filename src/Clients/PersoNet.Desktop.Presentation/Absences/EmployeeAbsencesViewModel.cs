using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Absences;
using PersoNet.Contracts.Absences;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Absences;

/// <summary>An absence kind with its Polish name, for the drop-down list.</summary>
public sealed record AbsenceKindChoice(AbsenceKind Kind, string Name);

/// <summary>One absence as shown in the list.</summary>
public sealed record AbsenceRow(AbsenceResponse Absence, string KindName, string Period, string DaysText, bool IsSickness);

/// <summary>
/// "Nieobecności" tab of an employee's card: sickness (L4), business trips, training, remote work and other absences that
/// are not leave, a year at a time, the latest first. One is added or changed in a dialog; removal needs a second click.
/// </summary>
public sealed partial class EmployeeAbsencesViewModel : EditorViewModel
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private readonly IAbsencesApi _api;
    private readonly TimeProvider _time;
    private readonly IDialogService _dialogs;
    private EmployeeResponse? _employee;
    private int _loadVersion;

    public EmployeeAbsencesViewModel(IAbsencesApi api, TimeProvider time, IDialogService dialogs, bool canManage)
    {
        ArgumentNullException.ThrowIfNull(time);

        _api = api;
        _time = time;
        _dialogs = dialogs;
        CanManage = canManage;
        Year = time.GetLocalNow().Year;
    }

    public static IReadOnlyList<AbsenceKindChoice> Kinds { get; } =
    [
        new(AbsenceKind.Sickness, AbsenceTexts.Name(AbsenceKind.Sickness)),
        new(AbsenceKind.ChildCareAllowance, AbsenceTexts.Name(AbsenceKind.ChildCareAllowance)),
        new(AbsenceKind.FamilyCareAllowance, AbsenceTexts.Name(AbsenceKind.FamilyCareAllowance)),
        new(AbsenceKind.WorkAccident, AbsenceTexts.Name(AbsenceKind.WorkAccident)),
        new(AbsenceKind.Rehabilitation, AbsenceTexts.Name(AbsenceKind.Rehabilitation)),
        new(AbsenceKind.BusinessTrip, AbsenceTexts.Name(AbsenceKind.BusinessTrip)),
        new(AbsenceKind.Training, AbsenceTexts.Name(AbsenceKind.Training)),
        new(AbsenceKind.TradeFair, AbsenceTexts.Name(AbsenceKind.TradeFair)),
        new(AbsenceKind.RemoteWork, AbsenceTexts.Name(AbsenceKind.RemoteWork)),
        new(AbsenceKind.MedicalExam, AbsenceTexts.Name(AbsenceKind.MedicalExam)),
        new(AbsenceKind.OtherExcused, AbsenceTexts.Name(AbsenceKind.OtherExcused)),
        new(AbsenceKind.Unexcused, AbsenceTexts.Name(AbsenceKind.Unexcused)),
    ];

    /// <summary>False for someone who may only look: no adding, changing or removing.</summary>
    public bool CanManage { get; }

    public ObservableCollection<AbsenceRow> Items { get; } = [];

    [ObservableProperty]
    public partial int Year { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>Days of the year by kind, e.g. "Choroba (L4): 12 dni · Praca zdalna: 40 dni".</summary>
    [ObservableProperty]
    public partial string? Summary { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(EditCommand))]
    public partial AbsenceRow? Selected { get; set; }

    /// <summary>First click on "Usuń" asks for confirmation; the second one deletes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteText))]
    public partial bool IsConfirmingDelete { get; set; }

    public string DeleteText => IsConfirmingDelete ? "Na pewno usunąć?" : "Usuń";

    /// <summary>Shows the absences of the employee in the current year (nothing for a card that is not saved yet).</summary>
    public Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        Year = _time.GetLocalNow().Year;
        return LoadAsync(cancellationToken);
    }

    [RelayCommand]
    public Task PreviousYearAsync(CancellationToken cancellationToken)
    {
        Year--;
        return LoadAsync(cancellationToken);
    }

    [RelayCommand]
    public Task NextYearAsync(CancellationToken cancellationToken)
    {
        Year++;
        return LoadAsync(cancellationToken);
    }

    [RelayCommand]
    public Task NewAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee || !CanManage)
        {
            return Task.CompletedTask;
        }

        var today = _time.GetLocalNow().Date;
        var start = today.Year == Year ? today : new DateTime(Year, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        return EditInDialogAsync(new AbsenceDialogViewModel(_api, employee, row: null, start), cancellationToken);
    }

    /// <summary>Opens the picked absence in the form.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    public Task EditAsync(AbsenceRow? row, CancellationToken cancellationToken) => _employee is not { } employee || (row ?? Selected) is not { } absence || !CanManage
        ? Task.CompletedTask
        : EditInDialogAsync(new AbsenceDialogViewModel(_api, employee, absence, absence.Absence.StartDate.ToDateTime(TimeOnly.MinValue)), cancellationToken);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (Selected is not { } current)
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
            await _api.DeleteAsync(current.Absence.Id, cancellationToken);
            await ReloadAsync(selectId: null, cancellationToken);
        });
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    partial void OnSelectedChanged(AbsenceRow? value) => IsConfirmingDelete = false;

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var version = ++_loadVersion;
        Items.Clear();
        IsEmpty = false;
        Summary = null;
        Selected = null;

        if (_employee is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var absences = await _api.ListAsync(_employee.Id, Year, cancellationToken);

            // Another employee or year may have been picked while this one loaded.
            if (version == _loadVersion)
            {
                Fill(absences, selectId: null);
            }
        });
    }

    private async Task EditInDialogAsync(AbsenceDialogViewModel dialog, CancellationToken cancellationToken)
    {
        if (!await _dialogs.ShowAsync(dialog))
        {
            return;
        }

        // The saved absence may be in another year; the list follows it.
        if (dialog.Saved is { } saved && saved.StartDate.Year != Year && saved.EndDate.Year != Year)
        {
            Year = saved.StartDate.Year;
        }

        await RunAsync(() => ReloadAsync(dialog.Saved?.Id, cancellationToken));
    }

    private async Task ReloadAsync(Guid? selectId, CancellationToken cancellationToken)
    {
        if (_employee is { } employee)
        {
            ++_loadVersion;
            Fill(await _api.ListAsync(employee.Id, Year, cancellationToken), selectId);
        }
    }

    private bool CanEdit(AbsenceRow? row) => CanManage && (row ?? Selected) is not null;

    private bool CanDelete() => CanManage && Selected is not null;

    private void Fill(IReadOnlyList<AbsenceResponse> absences, Guid? selectId)
    {
        Items.Clear();
        foreach (var absence in absences)
        {
            Items.Add(ToRow(absence));
        }

        IsEmpty = Items.Count == 0;
        Selected = Items.FirstOrDefault(row => row.Absence.Id == selectId);

        // Only the days of this year count, also of an absence that began in the previous one.
        var first = new DateOnly(Year, 1, 1);
        var last = new DateOnly(Year, 12, 31);
        Summary = IsEmpty
            ? null
            : string.Join(" · ", absences
                .GroupBy(absence => absence.Kind)
                .OrderByDescending(group => group.Sum(absence => DaysIn(absence, first, last)))
                .Select(group => $"{AbsenceTexts.Name(group.Key)}: {AbsenceTexts.Days(group.Sum(absence => DaysIn(absence, first, last)))}"));
    }

    private static int DaysIn(AbsenceResponse absence, DateOnly first, DateOnly last)
    {
        var start = absence.StartDate > first ? absence.StartDate : first;
        var end = absence.EndDate < last ? absence.EndDate : last;
        return Math.Max(0, end.DayNumber - start.DayNumber + 1);
    }

    private static AbsenceRow ToRow(AbsenceResponse absence)
    {
        var from = absence.StartDate.ToString("d MMM yyyy", Polish);
        var period = absence.StartDate == absence.EndDate ? from : $"{from} – {absence.EndDate.ToString("d MMM yyyy", Polish)}";
        var days = absence.CalendarDays == absence.WorkingDays
            ? AbsenceTexts.Days(absence.CalendarDays)
            : $"{AbsenceTexts.Days(absence.CalendarDays)} (roboczych {absence.WorkingDays})";

        return new AbsenceRow(absence, AbsenceTexts.Name(absence.Kind), period, days, AbsenceTexts.IsSickness(absence.Kind));
    }
}
