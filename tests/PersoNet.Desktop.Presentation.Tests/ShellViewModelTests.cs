using PersoNet.Desktop.Presentation.Authentication;
using PersoNet.Desktop.Presentation.Employees;
using PersoNet.Desktop.Presentation.Home;
using PersoNet.Desktop.Presentation.Leave;
using PersoNet.Desktop.Presentation.Organization;
using PersoNet.Desktop.Presentation.Shell;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class ShellViewModelTests
{
    private readonly FakeDialogService _dialogs = new();

    private EmployeesViewModel Employees(FakeOrganizationApi organization) =>
        new(new FakeEmployeesApi(), organization, new FakeLeaveApi(), TimeProvider.System, _dialogs);

    private static HomeViewModel Home(FakeOrganizationApi organization) =>
        new(new FakeEmployeesApi(), organization, new FakeLeaveApi(), TimeProvider.System);

    private LeaveViewModel LeaveScreen() =>
        new(new FakeLeaveApi(), new FakeEmployeesApi(), TimeProvider.System, _dialogs);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sign_in_shows_the_user_and_loads_the_structure()
    {
        var api = new FakeOrganizationApi();
        api.Add("Dział IT", "IT");
        var shell = new ShellViewModel(new FakeSignIn(), Home(api), Employees(api), LeaveScreen(), new DepartmentsViewModel(api, _dialogs), new PositionsViewModel(api, _dialogs));

        await shell.SignInAsync(Cancellation);

        shell.IsSignedIn.ShouldBeTrue();
        shell.User!.Name.ShouldBe("Anna Kowalska");
        shell.Departments.Roots.Single().Department.Code.ShouldBe("IT");
    }

    [Fact]
    public async Task Cancelled_sign_in_shows_a_message_and_stays_signed_out()
    {
        var api = new FakeOrganizationApi();
        var shell = new ShellViewModel(new FakeSignIn { Cancel = true }, Home(api), Employees(api), LeaveScreen(), new DepartmentsViewModel(api, _dialogs), new PositionsViewModel(api, _dialogs));

        await shell.SignInAsync(Cancellation);

        shell.IsSignedIn.ShouldBeFalse();
        shell.ErrorMessage.ShouldBe("Logowanie zostało przerwane.");
    }

    [Fact]
    public async Task Sign_out_forgets_the_user()
    {
        var api = new FakeOrganizationApi();
        var shell = new ShellViewModel(new FakeSignIn(), Home(api), Employees(api), LeaveScreen(), new DepartmentsViewModel(api, _dialogs), new PositionsViewModel(api, _dialogs));
        await shell.SignInAsync(Cancellation);

        await shell.SignOutAsync(Cancellation);

        shell.IsSignedIn.ShouldBeFalse();
        shell.SignInCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task Sign_in_opens_the_start_screen_and_shortcuts_open_the_right_form()
    {
        var api = new FakeOrganizationApi();
        var shell = new ShellViewModel(new FakeSignIn(), Home(api), Employees(api), LeaveScreen(), new DepartmentsViewModel(api, _dialogs), new PositionsViewModel(api, _dialogs));

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

    private sealed class FakeSignIn : ISignInService
    {
        public bool Cancel { get; init; }

        public Task<SignedInUser> SignInAsync(CancellationToken cancellationToken) =>
            Cancel ? throw new OperationCanceledException() : Task.FromResult(new SignedInUser("Anna Kowalska", "anna@acme.test"));

        public Task SignOutAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
