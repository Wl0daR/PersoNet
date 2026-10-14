using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Leave;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>
/// "Kalendarz urlopów" screen: who is on leave in a month, a row per person and a column per day, for the whole company
/// or a department with the departments under it. Approved leave and requests still waiting for a decision are shown.
/// </summary>
public sealed partial class TeamCalendarViewModel(ILeaveApi leave, IOrganizationApi organization, TimeProvider time) : EditorViewModel
{
    public static readonly DepartmentChoice WholeCompany = new(null, "Cała firma");

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");
    private static readonly string[] DayNames = ["nd", "pn", "wt", "śr", "cz", "pt", "sb"];

    private int _loadVersion;

    /// <summary>"Cała firma" and every active department, indented under its parent.</summary>
    public ObservableCollection<DepartmentChoice> Departments { get; } = [WholeCompany];

    /// <summary>The days of the shown month; the view makes a column of each.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<TeamCalendarDay> Days { get; private set; } = [];

    public ObservableCollection<TeamCalendarRow> Rows { get; } = [];

    [ObservableProperty]
    public partial DepartmentChoice Department { get; set; } = WholeCompany;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(MonthTitle), nameof(IsCurrentMonth))]
    public partial DateOnly Month { get; set; } = FirstOf(DateOnly.FromDateTime(time.GetLocalNow().DateTime));

    /// <summary>"Czerwiec 2025".</summary>
    public string MonthTitle => LeaveCalendar.MonthTitle(Month);

    public bool IsCurrentMonth => Month == FirstOf(Today);

    [ObservableProperty]
    public partial string Summary { get; private set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsEmpty { get; private set; }

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        if (Departments.Count == 1)
        {
            await LoadDepartmentsAsync(cancellationToken);
        }

        await LoadMonthAsync(cancellationToken);
    });

    [RelayCommand]
    public Task PreviousMonthAsync(CancellationToken cancellationToken) => ShowAsync(Month.AddMonths(-1), cancellationToken);

    [RelayCommand]
    public Task NextMonthAsync(CancellationToken cancellationToken) => ShowAsync(Month.AddMonths(1), cancellationToken);

    [RelayCommand]
    public Task CurrentMonthAsync(CancellationToken cancellationToken) => ShowAsync(FirstOf(Today), cancellationToken);

    /// <summary>Short mark of a kind of leave, so the grid does not rely on colour: W, Ż, O, B, D, S.</summary>
    public static string Mark(LeaveKind kind) => kind switch
    {
        LeaveKind.Annual => "W",
        LeaveKind.OnDemand => "Ż",
        LeaveKind.Occasional => "O",
        LeaveKind.Unpaid => "B",
        LeaveKind.ChildCare => "D",
        LeaveKind.ForceMajeure => "S",
        _ => "?",
    };

    /// <summary>The marks with the kinds they stand for, for the legend.</summary>
    public static IReadOnlyList<LeaveMarkLegend> Legend { get; } = [.. LeaveTexts.Kinds.Select(kind => new LeaveMarkLegend(Mark(kind.Kind), kind.Name))];

    protected override bool TryShowOnField(ApiException exception) => false;

    partial void OnDepartmentChanged(DepartmentChoice value) => _ = RunAsync(() => LoadMonthAsync(CancellationToken.None));

    private Task ShowAsync(DateOnly month, CancellationToken cancellationToken)
    {
        Month = month;
        return RunAsync(() => LoadMonthAsync(cancellationToken));
    }

    private async Task LoadDepartmentsAsync(CancellationToken cancellationToken)
    {
        var departments = await organization.GetDepartmentsAsync(includeInactive: false, cancellationToken);
        var children = departments.ToLookup(department => department.ParentId);
        var comparer = StringComparer.Create(Polish, ignoreCase: true);

        void Add(Guid? parentId, int depth)
        {
            foreach (var department in children[parentId].OrderBy(department => department.Name, comparer))
            {
                Departments.Add(new DepartmentChoice(department.Id, new string(' ', depth * 4) + department.Name));
                Add(department.Id, depth + 1);
            }
        }

        // Departments whose parent is inactive (so not listed) start a branch of their own.
        var listed = departments.Select(department => department.Id).ToHashSet();
        foreach (var root in departments.Where(department => department.ParentId is null || !listed.Contains(department.ParentId.Value))
                     .OrderBy(department => department.Name, comparer))
        {
            Departments.Add(new DepartmentChoice(root.Id, root.Name));
            Add(root.Id, 1);
        }
    }

    private async Task LoadMonthAsync(CancellationToken cancellationToken)
    {
        var version = ++_loadVersion;
        var month = Month;
        var calendar = await leave.GetCalendarAsync(month.Year, month.Month, Department.Id, cancellationToken);
        if (version != _loadVersion)
        {
            return;
        }

        var today = Today;
        var holidays = calendar.Holidays.ToDictionary(holiday => holiday.Date, holiday => holiday.Name);
        Days = [.. Enumerable.Range(0, DateTime.DaysInMonth(month.Year, month.Month))
            .Select(offset => month.AddDays(offset))
            .Select(date => new TeamCalendarDay(date, DayNames[(int)date.DayOfWeek], holidays.GetValueOrDefault(date), date == today))];

        Rows.Clear();
        foreach (var person in calendar.People)
        {
            Rows.Add(new TeamCalendarRow(
                $"{person.LastName} {person.FirstName}",
                string.Join(" · ", new[] { person.PositionName, person.DepartmentName }.Where(text => !string.IsNullOrEmpty(text))),
                [.. Days.Select(day => Cell(person, day))]));
        }

        IsEmpty = Rows.Count == 0;
        var away = IsCurrentMonth ? Rows.Count(row => row.Cells.Any(cell => cell.Day.IsToday && cell.IsApproved)) : (int?)null;
        Summary = $"{People(Rows.Count)}" + (away is { } count ? $" · dziś na urlopie: {count}" : string.Empty);
    }

    private static TeamCalendarCell Cell(LeaveCalendarPerson person, TeamCalendarDay day)
    {
        // Leave counts working days only, so a weekend or holiday inside it is not marked.
        var entry = day.IsDayOff ? null : person.Leave
            .Where(entry => entry.StartDate <= day.Date && entry.EndDate >= day.Date)
            .OrderBy(entry => entry.State == LeaveState.Approved ? 0 : 1)
            .FirstOrDefault();

        return new TeamCalendarCell(day, entry);
    }

    private static string People(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        var word = count == 1 ? "osoba" : last is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? "osoby" : "osób";
        return $"{count} {word}";
    }

    private static DateOnly FirstOf(DateOnly date) => new(date.Year, date.Month, 1);
}

/// <summary>A department to show, or the whole company when <see cref="Id"/> is null.</summary>
public sealed record DepartmentChoice(Guid? Id, string Name);

/// <summary>A column of the calendar.</summary>
public sealed record TeamCalendarDay(DateOnly Date, string DayName, string? Holiday, bool IsToday)
{
    public int Day => Date.Day;

    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public bool IsDayOff => IsWeekend || Holiday is not null;

    /// <summary>"2" over "pn" in the column header.</summary>
    public string Header => $"{Date.Day}\n{DayName}";

    public string ToolTip => Holiday ?? (IsWeekend ? "Weekend" : Date.ToString("dddd, d MMMM", CultureInfo.GetCultureInfo("pl-PL")));
}

/// <summary>A person's row: one cell per day of the month.</summary>
public sealed record TeamCalendarRow(string Name, string Details, IReadOnlyList<TeamCalendarCell> Cells);

/// <summary>What the person has on the day: a leave mark, or nothing.</summary>
public sealed record TeamCalendarCell(TeamCalendarDay Day, LeaveCalendarEntry? Entry)
{
    public string Mark => Entry is { } entry ? TeamCalendarViewModel.Mark(entry.Kind) : string.Empty;

    public bool IsApproved => Entry?.State == LeaveState.Approved;

    public bool IsPending => Entry?.State == LeaveState.Pending;

    public bool IsDayOff => Day.IsDayOff;

    /// <summary>"2 czerwca: urlop wypoczynkowy, oczekuje", also read by screen readers.</summary>
    public string Description
    {
        get
        {
            var date = Day.Date.ToString("d MMMM", CultureInfo.GetCultureInfo("pl-PL"));
            if (Entry is { } entry)
            {
                var hours = entry.IsHourly ? $" ({LeaveTexts.Hours(entry.Hours)})" : string.Empty;
                var waiting = entry.State == LeaveState.Pending ? ", czeka na decyzję" : string.Empty;
                return $"{date}: {LeaveTexts.KindName(entry.Kind).ToLower(CultureInfo.GetCultureInfo("pl-PL"))}{hours}{waiting}";
            }

            return Day.Holiday is { } holiday ? $"{date}: {holiday}" : Day.IsWeekend ? $"{date}: weekend" : $"{date}: w pracy";
        }
    }
}

/// <summary>A mark and the kind of leave it stands for.</summary>
public sealed record LeaveMarkLegend(string Mark, string Name);
