using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Leave;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>
/// "Urlop" tab of an employee's card: a month calendar with the employee's leave and days off, where HR can add a request
/// from a picked day; the annual leave entitlement of the year (set by HR), the balance, and the requests in that year.
/// </summary>
public sealed partial class EmployeeLeaveViewModel : EditorViewModel
{
    public const int MaxDays = 60;

    private readonly ILeaveApi _api;
    private readonly TimeProvider _time;
    private readonly Dictionary<int, IReadOnlyDictionary<DateOnly, string>> _holidays = [];

    private EmployeeResponse? _employee;
    private int _loadVersion;
    private int _loadedYear;

    // Days are rebuilt (new objects) when anything changes; re-selecting the same date then is not a click.
    private bool _isRebuilding;

    public EmployeeLeaveViewModel(ILeaveApi api, TimeProvider time)
    {
        _api = api;
        _time = time;
        Month = FirstOfMonth(Today);
        Form = new LeaveFormViewModel(api);

        // The period being filled in is highlighted in the calendar.
        Form.PropertyChanged += (_, e) =>
        {
            if (IsAdding && e.PropertyName is nameof(LeaveFormViewModel.EditStartDate) or nameof(LeaveFormViewModel.EditEndDate) or nameof(LeaveFormViewModel.IsHourly))
            {
                Rebuild();
            }
        };

        Rebuild();
    }

    public ObservableCollection<LeaveRow> Items { get; } = [];

    /// <summary>Six weeks, Monday first, covering <see cref="Month"/>.</summary>
    public ObservableCollection<CalendarDay> Days { get; } = [];

    public static IReadOnlyList<string> WeekdayNames => LeaveCalendar.WeekdayNames;

    /// <summary>Form of a new request for this employee, shown under the calendar while adding.</summary>
    public LeaveFormViewModel Form { get; }

    /// <summary>First day of the month shown in the calendar.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthTitle), nameof(Year))]
    public partial DateOnly Month { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddOnSelectedDay))]
    public partial CalendarDay? SelectedDay { get; set; }

    /// <summary>True while the new request form is open.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanAddOnSelectedDay))]
    public partial bool IsAdding { get; set; }

    /// <summary>The year of the month shown: the entitlement, balance and request list belong to it.</summary>
    public int Year => Month.Year;

    public string MonthTitle => LeaveCalendar.MonthTitle(Month);

    /// <summary>A working day without leave is picked, so a request can start on it.</summary>
    public bool CanAddOnSelectedDay => !IsAdding && SelectedDay is { HasLeave: false, IsDayOff: false };

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().Date);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary), nameof(HasEntitlement), nameof(RemainingText), nameof(TotalText))]
    public partial LeaveBalanceResponse? Balance { get; set; }

    /// <summary>Days of the yearly entitlement, as typed.</summary>
    [ObservableProperty]
    public partial string EditDays { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditCarriedOver { get; set; } = "0";

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
        IsAdding = false;
        Month = FirstOfMonth(Today);
        Items.Clear();
        Balance = null;
        EditDays = string.Empty;
        EditCarriedOver = "0";
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

    /// <summary>Opens the request form starting on the picked day (or today).</summary>
    [RelayCommand]
    public void AddLeave()
    {
        if (_employee is not { } employee)
        {
            return;
        }

        var start = (SelectedDay?.Date ?? Today).ToDateTime(TimeOnly.MinValue);
        Form.Reset(new Choice(employee.Id, $"{employee.LastName} {employee.FirstName}"), start, start, employeeFixed: true);
        IsAdding = true;
        Rebuild();
    }

    [RelayCommand]
    public void CancelAdding()
    {
        IsAdding = false;
        Rebuild();
    }

    [RelayCommand]
    public async Task SubmitLeaveAsync(CancellationToken cancellationToken)
    {
        if (await Form.SubmitAsync(cancellationToken) is not { } created)
        {
            return;
        }

        IsAdding = false;
        Month = FirstOfMonth(created.StartDate);
        await LoadAsync(cancellationToken);
        SelectedDay = Days.FirstOrDefault(day => day.Date == created.StartDate);
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

            Show(balance);
            Items.Clear();
            foreach (var request in requests)
            {
                Items.Add(LeaveTexts.ToRow(request));
            }

            IsEmpty = Items.Count == 0;
            Rebuild();
        });
    }

    [RelayCommand]
    public Task SaveEntitlementAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee || !Validate(out var days, out var carriedOver))
        {
            return Task.CompletedTask;
        }

        return RunAsync(async () => Show(await _api.SetEntitlementAsync(employee.Id, Year, new SaveLeaveEntitlementRequest(days, carriedOver), cancellationToken)));
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Code == "leave.entitlement_invalid")
        {
            SetError(nameof(EditDays), exception.Message);
            return true;
        }

        return false;
    }

    // A month of another year needs that year's leave, balance and holidays.
    partial void OnMonthChanged(DateOnly value)
    {
        Rebuild();
        if (_employee is not null && value.Year != _loadedYear)
        {
            _ = LoadAsync(CancellationToken.None);
        }
    }

    // While adding, a click on a later day sets the end of the leave; an earlier day (or any day for leave in hours) its start.
    partial void OnSelectedDayChanged(CalendarDay? value)
    {
        if (_isRebuilding || !IsAdding || value is null)
        {
            return;
        }

        var date = value.Date.ToDateTime(TimeOnly.MinValue);
        if (!Form.IsHourly && Form.EditStartDate is { } start && date > start)
        {
            Form.EditEndDate = date;
        }
        else
        {
            Form.EditStartDate = date;
        }
    }

    private static DateOnly FirstOfMonth(DateOnly day) => new(day.Year, day.Month, 1);

    private void Rebuild()
    {
        (DateOnly, DateOnly)? draft = IsAdding && Form.EditStartDate is { } start
            ? (DateOnly.FromDateTime(start), DateOnly.FromDateTime(Form.IsHourly ? start : Form.EditEndDate ?? start))
            : null;

        var days = LeaveCalendar.Build(Month, Today, _holidays.GetValueOrDefault(Month.Year) ?? new Dictionary<DateOnly, string>(), Items, draft);
        var selected = SelectedDay?.Date;

        _isRebuilding = true;
        try
        {
            Days.Clear();
            foreach (var day in days)
            {
                Days.Add(day);
            }

            SelectedDay = Days.FirstOrDefault(day => day.Date == selected);
        }
        finally
        {
            _isRebuilding = false;
        }
    }

    private void Show(LeaveBalanceResponse balance)
    {
        Balance = balance;
        EditDays = balance.HasEntitlement ? balance.EntitlementDays.ToString(CultureInfo.InvariantCulture) : string.Empty;
        EditCarriedOver = balance.CarriedOverDays.ToString(CultureInfo.InvariantCulture);
        ClearAllErrors();
    }

    private bool Validate(out int days, out int carriedOver)
    {
        var daysOk = int.TryParse(EditDays.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out days) && days <= MaxDays;
        var carriedOk = int.TryParse(string.IsNullOrWhiteSpace(EditCarriedOver) ? "0" : EditCarriedOver.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out carriedOver)
            && carriedOver <= MaxDays;

        SetError(nameof(EditDays), daysOk ? null : "Podaj liczbę dni od 0 do 60, np. 20 albo 26.");
        SetError(nameof(EditCarriedOver), carriedOk ? null : "Podaj liczbę dni od 0 do 60 albo zostaw 0.");

        return daysOk && carriedOk;
    }
}
