using PersoNet.Desktop.Presentation.Access;
using PersoNet.Desktop.Presentation.Authentication;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Company;
using PersoNet.Desktop.Presentation.Employees;
using PersoNet.Desktop.Presentation.Home;
using PersoNet.Desktop.Presentation.Leave;
using PersoNet.Desktop.Presentation.Organization;
using PersoNet.Desktop.Presentation.Shell;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class ShellViewModelTests
{
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeThemeService _theme = new();
    private readonly FakeAccessApi _access = new();
    private readonly UserAccess _user = new();

    private EmployeesViewModel Employees(FakeOrganizationApi organization) =>
        new(new FakeEmployeesApi(), organization, new FakeLeaveApi(), _access, _user, TimeProvider.System, _dialogs);

    private HomeViewModel Home(FakeOrganizationApi organization) =>
        new(new FakeEmployeesApi(), organization, new FakeLeaveApi(), _user, TimeProvider.System);

    private ShellViewModel Shell(FakeOrganizationApi api, FakeSignIn? signIn = null) => new(
        signIn ?? new FakeSignIn(), Home(api), Employees(api), LeaveScreen(), new TeamCalendarViewModel(new FakeLeaveApi(), api, TimeProvider.System), new DepartmentsViewModel(api, _dialogs), new PositionsViewModel(api, _dialogs),
        new RolesViewModel(_access, _dialogs), new CompanyViewModel(new FakeCompanyApi(), _dialogs), _access, _user, _theme);

    private LeaveViewModel LeaveScreen() =>
        new(new FakeLeaveApi(), new FakeEmployeesApi(), TimeProvider.System, _dialogs);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sign_in_shows_the_user_and_loads_the_structure()
    {
        var api = new FakeOrganizationApi();
        api.Add("Dział IT", "IT");
        var shell = Shell(api);

        await shell.SignInAsync(Cancellation);

        shell.IsSignedIn.ShouldBeTrue();
        shell.User!.Name.ShouldBe("Anna Kowalska");
        shell.Departments.Roots.Single().Department.Code.ShouldBe("IT");
    }

    [Fact]
    public async Task Cancelled_sign_in_shows_a_message_and_stays_signed_out()
    {
        var api = new FakeOrganizationApi();
        var shell = Shell(api, new FakeSignIn { Cancel = true });

        await shell.SignInAsync(Cancellation);

        shell.IsSignedIn.ShouldBeFalse();
        shell.ErrorMessage.ShouldBe("Logowanie zostało przerwane.");
    }

    [Fact]
    public async Task Sign_out_forgets_the_user()
    {
        var api = new FakeOrganizationApi();
        var shell = Shell(api);
        await shell.SignInAsync(Cancellation);

        await shell.SignOutAsync(Cancellation);

        shell.IsSignedIn.ShouldBeFalse();
        shell.SignInCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task Sign_in_opens_the_start_screen_and_shortcuts_open_the_right_form()
    {
        var api = new FakeOrganizationApi();
        var shell = Shell(api);

        await shell.SignInAsync(Cancellation);

        shell.CurrentItem.ShouldBe(shell.HomeItem);
        shell.UserInitials.ShouldBe("AK");
        shell.Home.Greeting.ShouldEndWith("Anna");

        shell.AddEmployee();
        shell.CurrentItem.ShouldBe(shell.EmployeesItem);
        _dialogs.Shown.OfType<EmployeeCardViewModel>().ShouldHaveSingleItem().IsNew.ShouldBeTrue();

        shell.Navigate(shell.PositionsItem);
        shell.CurrentItem.Page.ShouldBe(shell.Positions);
    }

    [Fact]
    public async Task Side_menu_lists_only_the_screens_the_user_may_work_on()
    {
        var api = new FakeOrganizationApi();
        _access.Permissions.Clear();
        _access.Permissions.AddRange([UserAccess.LeaveRead, UserAccess.LeaveManage]);
        var shell = Shell(api);

        await shell.SignInAsync(Cancellation);

        shell.Navigation.ShouldBe([shell.HomeItem, shell.LeaveItem, shell.CalendarItem]);
        shell.Home.CanSeeEmployees.ShouldBeFalse();
        shell.Home.CanAddLeave.ShouldBeTrue();
        shell.Navigate(shell.RolesItem);
        shell.CurrentItem.ShouldBe(shell.HomeItem);

        await shell.SignOutAsync(Cancellation);
        _access.Permissions.AddRange(UserAccessFor.Everything);
        await shell.SignInAsync(Cancellation);

        shell.Navigation.ShouldBe([shell.HomeItem, shell.EmployeesItem, shell.LeaveItem, shell.CalendarItem, shell.DepartmentsItem, shell.PositionsItem, shell.RolesItem, shell.CompanyItem]);
    }

    private sealed class FakeSignIn : ISignInService
    {
        public bool Cancel { get; init; }

        public Task<SignedInUser> SignInAsync(CancellationToken cancellationToken) =>
            Cancel ? throw new OperationCanceledException() : Task.FromResult(new SignedInUser("Anna Kowalska", "anna@acme.test"));

        public Task SignOutAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    [Fact]
    public void Theme_menu_marks_the_remembered_theme_and_switching_applies_the_new_one()
    {
        var api = new FakeOrganizationApi();
        _theme.Theme = AppTheme.Dark;
        var shell = Shell(api);

        shell.ThemeText.ShouldBe("Motyw: Ciemny");
        shell.Themes.Single(choice => choice.IsSelected).Theme.ShouldBe(AppTheme.Dark);

        shell.ChangeTheme(shell.Themes.Single(choice => choice.Theme == AppTheme.Light));

        _theme.Theme.ShouldBe(AppTheme.Light);
        shell.ThemeText.ShouldBe("Motyw: Jasny");
        shell.Themes.Single(choice => choice.IsSelected).Theme.ShouldBe(AppTheme.Light);
    }

    private sealed class FakeThemeService : IThemeService
    {
        public AppTheme Theme { get; set; }

        public void SetTheme(AppTheme theme) => Theme = theme;
    }
}
