using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Organization;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Home;

/// <summary>
/// "Start" screen: a greeting, the size of the company at a glance and shortcuts to the most common tasks.
/// </summary>
public sealed partial class HomeViewModel(IEmployeesApi employees, IOrganizationApi organization, TimeProvider time) : EditorViewModel
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Greeting))]
    public partial string? UserName { get; set; }

    [ObservableProperty]
    public partial int EmployedCount { get; set; }

    [ObservableProperty]
    public partial int FormerCount { get; set; }

    [ObservableProperty]
    public partial int DepartmentCount { get; set; }

    [ObservableProperty]
    public partial int PositionCount { get; set; }

    /// <summary>True once the numbers are loaded, so the view does not show zeros while waiting.</summary>
    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    public string Greeting
    {
        get
        {
            var hour = time.GetLocalNow().Hour;
            var hello = hour is >= 18 or < 5 ? "Dobry wieczór" : "Dzień dobry";
            var firstName = UserName?.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();

            return firstName is null ? hello : $"{hello}, {firstName}";
        }
    }

    /// <summary>Today's date in words, e.g. "poniedziałek, 13 października 2026".</summary>
    public string Today => time.GetLocalNow().ToString("dddd, d MMMM yyyy", Polish);

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        // Page size 1: only the totals are needed.
        var employed = await employees.ListAsync(new EmployeeListQuery(null, null, IncludeFormer: false, Page: 1, PageSize: 1), cancellationToken);
        var everyone = await employees.ListAsync(new EmployeeListQuery(null, null, IncludeFormer: true, Page: 1, PageSize: 1), cancellationToken);
        var departments = await organization.GetDepartmentsAsync(includeInactive: false, cancellationToken);
        var positions = await organization.GetPositionsAsync(includeInactive: false, cancellationToken);

        EmployedCount = employed.TotalCount;
        FormerCount = everyone.TotalCount - employed.TotalCount;
        DepartmentCount = departments.Count;
        PositionCount = positions.Count;
        IsLoaded = true;
        OnPropertyChanged(nameof(Today));
    });

    protected override bool TryShowOnField(ApiException exception) => false;
}
