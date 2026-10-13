using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Leave;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>
/// "Urlop" tab of an employee's card: a month calendar with the employee's leave and days off, the annual leave left
/// in that year and the year's requests. A new request, a request's details and the entitlement open in dialogs.
/// </summary>
public sealed partial class EmployeeLeaveViewModel : EditorViewModel
{
    private readonly ILeaveApi _api;
    private readonly TimeProvider _time;
    private readonly IDialogService _dialogs;
    private readonly Dictionary<int, IReadOnlyDictionary<DateOnly, string>> _holidays = [];

    private EmployeeResponse? _employee;
    private int _loadVersion;
    private int _loadedYear;

    public EmployeeLeaveViewModel(ILeaveApi api, TimeProvider time, IDialogService dialogs)
    {
        _api = api;
        _time = time;
        _dialogs = dialogs;
        Month = FirstOfMonth(Today);
        Rebuild();
    }

    public ObservableCollection<LeaveRow> Items { get; } = [];

    /// <summary>Six weeks, Monday first, covering <see cref="Month"/>.</summary>
    public ObservableCollection<CalendarDay> Days { get; } = [];

    public static IReadOnlyList<string> WeekdayNames => LeaveCalendar.WeekdayNames;

    /// <summary>First day of the month shown in the calendar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthTitle), nameof(Year))]
    public partial DateOnly Month { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddOnSelectedDay))]
    [NotifyCanExecuteChangedFor(nameof(OpenDayCommand))]
    public partial CalendarDay? SelectedDay { get; set; }

    /// <summary>The year of the month shown: the entitlement, balance and request list belong to it.</summary>
    public int Year => Month.Year;

    public string MonthTitle => LeaveCalendar.MonthTitle(Month);

    /// <summary>A working day without leave is picked, so a request can start on it.</summary>
    public bool CanAddOnSelectedDay => SelectedDay is { HasLeave: false, IsDayOff: false };

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().Date);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary), nameof(HasEntitlement), nameof(RemainingText), nameof(TotalText))]
    public partial LeaveBalanceResponse? Balance { get; set; }

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public bool HasEntitlement => Balance?.HasEntitlement == true;

    /// <summary>What is left, e.g. "13 dni 4 godz."</summary>
    public string RemainingText => Balance is { HasEntitlement: true } balance ? LeaveTexts.Amount(balance.RemainingHours) : string.Empty;

    /// <summary>The whole entitlement with carried-over days, e.g. "z 26 dni".</summary>
    public string TotalText => Balance is { HasEntitlement: true } balance ? $"z {LeaveTexts.Amount(balance.TotalHours)}" : string.Empty;

    /// <summary>"Zostało 14 dni" with the parts it is made of.</summary>
    public string Summary => Balance is not { } balance
        ? string.Empty
        : !balance.HasEntitlement
            ? $"Wymiar urlopu na {balance.Year} nie jest jeszcze ustawiony. Zwykle 20 dni, a po 10 latach pracy (z nauką) 26 dni; przy niepełnym etacie proporcjonalnie mniej."
            : $"Wykorzystano {LeaveTexts.Amount(balance.UsedHours)}, {LeaveTexts.Amount(balance.PendingHours)} czeka na decyzję, na żądanie {LeaveTexts.Days(balance.OnDemandDays)} z 4."
                + (balance.DailyHours != LeaveTexts.HoursPerDay ? $" Niepełny etat: dzień urlopu to {LeaveTexts.Hours(balance.DailyHours)}" : string.Empty);

    public async Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        _loadedYear = 0;
        Month = FirstOfMonth(Today);
        Items.Clear();
        Balance = null;
        ClearAllErrors();

        Rebuild();
        SelectedDay = Days.FirstOrDefault(day => day.IsToday);

        if (employee is not null)
        {
            await LoadAsync(cancellationToken);
        }
    }

    [RelayCommand]
    public void PreviousMonth() => Month = Month.AddMonths(-1);

    [RelayCommand]
    public void NextMonth() => Month = Month.AddMonths(1);

    [RelayCommand]
    public void ThisMonth()
    {
        Month = FirstOfMonth(Today);
        SelectedDay = Days.FirstOrDefault(day => day.IsToday);
    }

    /// <summary>Opens the form of a new request for this employee, starting on the picked day (or today).</summary>
    [RelayCommand]
    public async Task AddLeaveAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee)
        {
            return;
        }

        var start = (SelectedDay?.Date ?? Today).ToDateTime(TimeOnly.MinValue);
        var form = new LeaveFormViewModel(_api);
        form.Reset(new Choice(employee.Id, $"{employee.FirstName} {employee.LastName}"), start, start, employeeFixed: true);

        if (await _dialogs.ShowAsync(form) && form.Created is { } created)
        {
            Month = FirstOfMonth(created.StartDate);
            await LoadAsync(cancellationToken);
            SelectedDay = Days.FirstOrDefault(day => day.Date == created.StartDate);
        }
    }

    /// <summary>Double click on a day: its leave request, or a new one starting on that day.</summary>
    [RelayCommand(CanExecute = nameof(CanOpenDay))]
    public Task OpenDayAsync(CalendarDay? day, CancellationToken cancellationToken)
    {
        if ((day ?? SelectedDay) is not { } picked)
        {
            return Task.CompletedTask;
        }

        SelectedDay = Days.FirstOrDefault(candidate => candidate.Date == picked.Date) ?? picked;
        return picked.Leave is { } leave ? OpenRequestAsync(leave, cancellationToken)
            : picked.IsDayOff ? Task.CompletedTask
            : AddLeaveAsync(cancellationToken);
    }

    /// <summary>Opens a request with its details and decision.</summary>
    [RelayCommand]
    public async Task OpenRequestAsync(LeaveRow? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return;
        }

        var dialog = new LeaveDecisionViewModel(_api, row);
        await dialog.LoadAsync(cancellationToken);
        if (await _dialogs.ShowAsync(dialog))
        {
            await LoadAsync(cancellationToken);
        }
    }

    /// <summary>Opens the entitlement of the shown year.</summary>
    [RelayCommand]
    public async Task EditEntitlementAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee)
        {
            return;
        }

        var dialog = new EntitlementDialogViewModel(_api, employee, Year, Balance);
        if (await _dialogs.ShowAsync(dialog))
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

        var version = ++_loadVersion;
        var year = Year;
        _loadedYear = year;

        return RunAsync(async () =>
        {
            var balance = await _api.GetBalanceAsync(employee.Id, year, cancellationToken);
            var requests = await _api.ListAsync(state: null, year, employee.Id, cancellationToken);
            if (!_holidays.ContainsKey(year))
            {
                var holidays = await _api.GetHolidaysAsync(year, cancellationToken);
                _holidays[year] = holidays.ToDictionary(holiday => holiday.Date, holiday => holiday.Name);
            }

            // A newer employee or year may have been picked while this one loaded.
            if (version != _loadVersion)
            {
                return;
            }

            Balance = balance;
            Items.Clear();
            foreach (var request in requests)
            {
                Items.Add(LeaveTexts.ToRow(request));
            }

            IsEmpty = Items.Count == 0;
            Rebuild();
        });
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    // A month of another year needs that year's leave, balance and holidays.
    partial void OnMonthChanged(DateOnly value)
    {
        Rebuild();
        if (_employee is not null && value.Year != _loadedYear)
        {
            _ = LoadAsync(CancellationToken.None);
        }
    }

    private bool CanOpenDay(CalendarDay? day) => (day ?? SelectedDay) is not null;

    private static DateOnly FirstOfMonth(DateOnly day) => new(day.Year, day.Month, 1);

    private void Rebuild()
    {
        var days = LeaveCalendar.Build(Month, Today, _holidays.GetValueOrDefault(Month.Year) ?? new Dictionary<DateOnly, string>(), Items);
        var selected = SelectedDay?.Date;

        Days.Clear();
        foreach (var day in days)
        {
            Days.Add(day);
        }

        SelectedDay = Days.FirstOrDefault(day => day.Date == selected);
    }
}
