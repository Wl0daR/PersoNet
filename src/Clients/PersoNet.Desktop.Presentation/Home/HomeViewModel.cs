using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Leave;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Access;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Home;

/// <summary>
/// "Start" screen: a greeting, the size of the company at a glance and shortcuts to the most common tasks.
/// </summary>
public sealed partial class HomeViewModel(IEmployeesApi employees, IOrganizationApi organization, ILeaveApi leave, UserAccess user, TimeProvider time) : EditorViewModel
{
    // Tiles and shortcuts the signed-in user has the permissions for.
    public bool CanSeeEmployees => user.Can(UserAccess.EmployeesRead);

    public bool CanSeeLeave => user.Can(UserAccess.LeaveRead);

    public bool CanManageOrganization => user.Can(UserAccess.OrganizationManage);

    public bool CanAddEmployee => user.Can(UserAccess.EmployeesManage);

    public bool CanAddLeave => user.Can(UserAccess.LeaveManage);

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

    /// <summary>Leave requests of any year still waiting for a decision.</summary>
    [ObservableProperty]
    public partial int PendingLeaveCount { get; set; }

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
        OnPropertyChanged(string.Empty);

        if (CanSeeEmployees)
        {
            // Page size 1: only the totals are needed.
            var employed = await employees.ListAsync(new EmployeeListQuery(null, null, IncludeFormer: false, Page: 1, PageSize: 1), cancellationToken);
            var everyone = await employees.ListAsync(new EmployeeListQuery(null, null, IncludeFormer: true, Page: 1, PageSize: 1), cancellationToken);
            EmployedCount = employed.TotalCount;
            FormerCount = everyone.TotalCount - employed.TotalCount;
        }

        if (CanSeeLeave)
        {
            PendingLeaveCount = (await leave.ListAsync(LeaveState.Pending, year: null, employeeId: null, cancellationToken)).Count;
        }

        DepartmentCount = (await organization.GetDepartmentsAsync(includeInactive: false, cancellationToken)).Count;
        PositionCount = (await organization.GetPositionsAsync(includeInactive: false, cancellationToken)).Count;
        IsLoaded = true;
    });

    protected override bool TryShowOnField(ApiException exception) => false;
}
