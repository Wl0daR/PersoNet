using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.WorkTime;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.WorkTime;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Leave;
using PersoNet.Desktop.Presentation.Reports;

namespace PersoNet.Desktop.Presentation.WorkTime;

/// <summary>
/// "Czas pracy" tab of an employee's card: the month's timesheet ("karta ewidencji czasu pracy") day by day with the
/// month's totals. A day opens in a dialog; the month can be filled from the plan and saved to Excel.
/// </summary>
public sealed partial class EmployeeWorkTimeViewModel : EditorViewModel
{
    private readonly IWorkTimeApi _api;
    private readonly IFileService _files;
    private readonly TimeProvider _time;
    private readonly IDialogService _dialogs;

    private EmployeeResponse? _employee;
    private int _loadVersion;

    public EmployeeWorkTimeViewModel(IWorkTimeApi api, IFileService files, TimeProvider time, IDialogService dialogs, bool canEdit)
    {
        _api = api;
        _files = files;
        _time = time;
        _dialogs = dialogs;
        CanEdit = canEdit;
        Month = FirstOfMonth(Today);
    }

    /// <summary>False for someone who may only look at the timesheet.</summary>
    public bool CanEdit { get; }

    public ObservableCollection<WorkTimeRow> Rows { get; } = [];

    /// <summary>First day of the month shown.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthTitle))]
    public partial DateOnly Month { get; set; }

    public string MonthTitle => LeaveCalendar.MonthTitle(Month);

    [ObservableProperty]
    [NotifyPropertyChangedFor(
        nameof(NormText), nameof(WorkedText), nameof(OvertimeText), nameof(NightText), nameof(AbsenceText), nameof(NormHint),
        nameof(SourceText), nameof(ClockIdText), nameof(IssuesText), nameof(HasIssues), nameof(HasSheet))]
    [NotifyCanExecuteChangedFor(nameof(FillFromPlanCommand), nameof(ExportCommand), nameof(EditClockIdCommand))]
    public partial TimesheetResponse? Sheet { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenDayCommand), nameof(ClearDayCommand))]
    public partial WorkTimeRow? Selected { get; set; }

    /// <summary>The workbook saved last, so it can be opened.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExport))]
    [NotifyCanExecuteChangedFor(nameof(OpenExportCommand))]
    public partial string? ExportPath { get; private set; }

    public bool HasExport => ExportPath is not null;

    public bool HasSheet => Sheet is not null;

    /// <summary>The working time norm of the month (art. 130 KP), e.g. "144:00".</summary>
    public string NormText => Sheet is { } sheet ? WorkTimeTexts.Total(sheet.Totals.NormHours) : string.Empty;

    public string WorkedText => Sheet is { } sheet ? WorkTimeTexts.Total(sheet.Totals.WorkedHours) : string.Empty;

    /// <summary>Overtime and, for part-time, hours over the plan.</summary>
    public string OvertimeText => Sheet is not { Totals: var totals } ? string.Empty
        : totals.OverPlanHours > 0 ? $"{WorkTimeTexts.Total(totals.OvertimeHours)} + {WorkTimeTexts.Total(totals.OverPlanHours)} ponad etat"
        : WorkTimeTexts.Total(totals.OvertimeHours);

    public string NightText => Sheet is { } sheet ? WorkTimeTexts.Total(sheet.Totals.NightHours) : string.Empty;

    /// <summary>Leave and other absences together.</summary>
    public string AbsenceText => Sheet is { } sheet ? WorkTimeTexts.Total(sheet.Totals.LeaveHours + sheet.Totals.AbsenceHours) : string.Empty;

    /// <summary>Why the norm is lower than the planned days, when a holiday falls on Saturday; also work on days off.</summary>
    public string? NormHint
    {
        get
        {
            if (Sheet is not { Totals: var totals })
            {
                return null;
            }

            var hints = new List<string>();
            if (totals.NormHours < totals.PlannedHours)
            {
                hints.Add($"Święto w sobotę obniża wymiar o {WorkTimeTexts.Total(totals.PlannedHours - totals.NormHours)} godz. Wyznacz pracownikowi inny dzień wolny w tym miesiącu.");
            }

            if (totals.DayOffHours + totals.SundayHolidayHours > 0)
            {
                hints.Add($"Praca w dni wolne: {WorkTimeTexts.Total(totals.DayOffHours)} w soboty, {WorkTimeTexts.Total(totals.SundayHolidayHours)} w niedziele i święta.");
            }

            return hints.Count == 0 ? null : string.Join(" ", hints);
        }
    }

    public string SourceText => Sheet is { } sheet ? $"Źródło czasu pracy: {WorkTimeTexts.SourceName(sheet.Source)}" : string.Empty;

    public string ClockIdText => Sheet is not { } sheet ? string.Empty
        : sheet.ClockId is { } clockId ? $"Identyfikator RCP: {clockId}" : "Identyfikator RCP: nie ustawiono";

    public bool HasIssues => Sheet?.Totals.DaysWithIssues > 0;

    public string IssuesText => Sheet?.Totals.DaysWithIssues is { } count and > 0
        ? $"Dni z brakującym odbiciem: {count}. Otwórz dzień i wpisz godziny."
        : string.Empty;

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().Date);

    public async Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        Month = FirstOfMonth(Today);
        Sheet = null;
        Rows.Clear();
        ExportPath = null;
        ClearAllErrors();

        if (employee is not null)
        {
            await LoadAsync(cancellationToken);
        }
    }

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee)
        {
            return Task.CompletedTask;
        }

        // Months clicked through quickly: only the last answer is shown.
        var version = ++_loadVersion;
        var month = Month;
        return RunAsync(async () =>
        {
            var sheet = await _api.GetTimesheetAsync(employee.Id, month.Year, month.Month, cancellationToken);
            if (version == _loadVersion)
            {
                Show(sheet);
            }
        });
    }

    [RelayCommand]
    public Task PreviousMonthAsync(CancellationToken cancellationToken)
    {
        Month = Month.AddMonths(-1);
        return LoadAsync(cancellationToken);
    }

    [RelayCommand]
    public Task NextMonthAsync(CancellationToken cancellationToken)
    {
        Month = Month.AddMonths(1);
        return LoadAsync(cancellationToken);
    }

    [RelayCommand]
    public Task ThisMonthAsync(CancellationToken cancellationToken)
    {
        Month = FirstOfMonth(Today);
        return LoadAsync(cancellationToken);
    }

    /// <summary>Double click or Enter on a day: its hours or absence in a dialog.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenDay))]
    public async Task OpenDayAsync(WorkTimeRow? row)
    {
        if ((row ?? Selected) is not { } picked || _employee is not { } employee)
        {
            return;
        }

        var dialog = new WorkDayDialogViewModel(_api, employee.Id, picked.Day);
        if (await _dialogs.ShowAsync(dialog) && dialog.Saved is { } sheet)
        {
            Show(sheet);
            Selected = Rows.FirstOrDefault(candidate => candidate.Date == picked.Date);
        }
    }

    /// <summary>Fills the empty working days with the planned hours, so only the exceptions are left to type.</summary>
    [RelayCommand(CanExecute = nameof(CanChangeMonth))]
    public Task FillFromPlanAsync(CancellationToken cancellationToken) => _employee is not { } employee
        ? Task.CompletedTask
        : RunAsync(async () => Show(await _api.FillFromPlanAsync(employee.Id, Month.Year, Month.Month, cancellationToken)));

    /// <summary>Removes what was typed for the picked day; a day the clock recorded gets its hours from the clock again.</summary>
    [RelayCommand(CanExecute = nameof(CanClearDay))]
    public Task ClearDayAsync(CancellationToken cancellationToken)
    {
        if (Selected is not { } row || _employee is not { } employee)
        {
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            Show(await _api.ClearDayAsync(employee.Id, row.Date, cancellationToken));
            Selected = Rows.FirstOrDefault(candidate => candidate.Date == row.Date);
        });
    }

    [RelayCommand(CanExecute = nameof(CanChangeMonth))]
    public async Task EditClockIdAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee)
        {
            return;
        }

        var dialog = new ClockIdDialogViewModel(_api, employee.Id, Sheet?.ClockId);
        if (await _dialogs.ShowAsync(dialog))
        {
            await LoadAsync(cancellationToken);
        }
    }

    /// <summary>Saves the timesheet of the month to an Excel workbook, for payroll or the personnel file.</summary>
    [RelayCommand(CanExecute = nameof(HasSheet))]
    public void Export()
    {
        if (Sheet is not { } sheet)
        {
            return;
        }

        var fileName = $"Karta ewidencji {sheet.LastName} {sheet.FirstName} {sheet.Year}-{sheet.Month:00}.xlsx";
        if (_files.AskWhereToSaveWorkbook(fileName) is not { } path)
        {
            return;
        }

        ErrorMessage = null;
        try
        {
            using (var stream = File.Create(path))
            {
                ExcelExport.Write(stream, [TimesheetTable.For(sheet)]);
            }

            ExportPath = path;
        }
        catch (IOException exception)
        {
            ErrorMessage = $"Nie udało się zapisać pliku: {exception.Message} Zamknij go w Excelu, jeśli jest otwarty, i spróbuj ponownie.";
        }
        catch (UnauthorizedAccessException)
        {
            ErrorMessage = "Nie masz prawa zapisu w tym miejscu. Wybierz inny folder.";
        }
    }

    [RelayCommand(CanExecute = nameof(HasExport))]
    public void OpenExport()
    {
        if (ExportPath is { } path)
        {
            _files.Open(path);
        }
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    private bool CanOpenDay(WorkTimeRow? row) => CanEdit && (row ?? Selected) is { IsEmployed: true };

    private bool CanClearDay() => CanEdit && Selected is { IsRecorded: true };

    private bool CanChangeMonth() => CanEdit && Sheet is not null;

    private void Show(TimesheetResponse sheet)
    {
        Sheet = sheet;
        Month = new DateOnly(sheet.Year, sheet.Month, 1);
        Rows.Clear();
        foreach (var day in sheet.Days)
        {
            Rows.Add(new WorkTimeRow(day));
        }
    }

    private static DateOnly FirstOfMonth(DateOnly date) => new(date.Year, date.Month, 1);
}

/// <summary>One day of the timesheet as the grid shows it.</summary>
public sealed class WorkTimeRow(TimesheetDay day)
{
    public TimesheetDay Day => day;

    public DateOnly Date => day.Date;

    public string DayText => WorkTimeTexts.ShortDay(day.Date);

    public bool IsDayOff => day.Kind != TimesheetDayKind.Working;

    public bool IsEmployed => day.IsEmployed;

    public bool IsRecorded => day.IsRecorded;

    public bool HasIssue => day.Issue != ClockIssue.None;

    /// <summary>From–to, the absence, the leave, or the holiday's name.</summary>
    public string HoursText => day switch
    {
        { Absence: { } absence } => WorkTimeTexts.AbsenceName(absence),
        { Leave: { } leave, IsRecorded: false } => LeaveTexts.KindName(leave),
        { Start: { } start, End: { } end } => $"{WorkTimeTexts.Time(start)}–{WorkTimeTexts.Time(end)}" + (day.BreakMinutes > 0 ? $" (przerwa {day.BreakMinutes} min)" : string.Empty),
        { Start: { } start } => $"{WorkTimeTexts.Time(start)}–?",
        { End: { } end } => $"?–{WorkTimeTexts.Time(end)}",
        { IsEmployed: false } => "poza zatrudnieniem",
        { HolidayName: { } holiday } => holiday,
        _ => string.Empty,
    };

    public string PlannedText => WorkTimeTexts.Duration(day.PlannedHours);

    public string WorkedText => WorkTimeTexts.Duration(day.WorkedHours);

    public string OvertimeText => WorkTimeTexts.Duration(day.OvertimeHours + day.OverPlanHours);

    public string NightText => WorkTimeTexts.Duration(day.NightHours);

    /// <summary>A clock problem, then the note, then whether HR corrected the clock.</summary>
    public string StatusText => string.Join(" · ", new[]
    {
        WorkTimeTexts.IssueText(day.Issue),
        day.Leave is { } leave && day.IsRecorded ? LeaveTexts.KindName(leave) : null,
        day.Note,
        day.IsManual && day.Events.Count > 0 ? "poprawione ręcznie" : null,
    }.Where(part => !string.IsNullOrEmpty(part)));
}
