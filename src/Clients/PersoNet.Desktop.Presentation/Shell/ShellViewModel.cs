using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.Desktop.Presentation.Authentication;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Employees;
using PersoNet.Desktop.Presentation.Home;
using PersoNet.Desktop.Presentation.Organization;

namespace PersoNet.Desktop.Presentation.Shell;

/// <summary>
/// Main window of the HR desktop app: sign-in, then the module screens picked from the side menu.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ISignInService _signIn;

    public ShellViewModel(
        ISignInService signIn,
        HomeViewModel home,
        EmployeesViewModel employees,
        DepartmentsViewModel departments,
        PositionsViewModel positions)
    {
        _signIn = signIn;
        Home = home;
        Employees = employees;
        Departments = departments;
        Positions = positions;

        HomeItem = new NavigationItem("Start", "\uE80F", home);
        EmployeesItem = new NavigationItem("Pracownicy", "\uE716", employees);
        DepartmentsItem = new NavigationItem("Działy", "\uE8B7", departments);
        PositionsItem = new NavigationItem("Stanowiska", "\uE821", positions);
        Navigation = [HomeItem, EmployeesItem, DepartmentsItem, PositionsItem];
        CurrentItem = HomeItem;
    }

    [ObservableProperty]
    public partial string Title { get; set; } = "PersoNet – Kadry";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn), nameof(UserInitials))]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand), nameof(SignOutCommand))]
    public partial SignedInUser? User { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool IsSignedIn => User is not null;

    /// <summary>Initials of the signed-in user for the avatar in the side menu.</summary>
    public string UserInitials => Initials.Of(User?.Name);

    public HomeViewModel Home { get; }

    public EmployeesViewModel Employees { get; }

    public DepartmentsViewModel Departments { get; }

    public PositionsViewModel Positions { get; }

    public NavigationItem HomeItem { get; }

    public NavigationItem EmployeesItem { get; }

    public NavigationItem DepartmentsItem { get; }

    public NavigationItem PositionsItem { get; }

    public IReadOnlyList<NavigationItem> Navigation { get; }

    /// <summary>The screen picked in the side menu.</summary>
    [ObservableProperty]
    public partial NavigationItem CurrentItem { get; set; }

    [RelayCommand]
    public void Navigate(NavigationItem? item)
    {
        if (item is not null)
        {
            CurrentItem = item;
        }
    }

    /// <summary>Shortcut from the start screen: an empty employee card.</summary>
    [RelayCommand]
    public void AddEmployee()
    {
        CurrentItem = EmployeesItem;
        Employees.NewCommand.Execute(null);
    }

    /// <summary>Shortcut from the start screen: an empty department form.</summary>
    [RelayCommand]
    public void AddDepartment()
    {
        CurrentItem = DepartmentsItem;
        Departments.Selected = null;
        Departments.NewCommand.Execute(null);
    }

    /// <summary>Shortcut from the start screen: an empty position form.</summary>
    [RelayCommand]
    public void AddPosition()
    {
        CurrentItem = PositionsItem;
        Positions.NewCommand.Execute(null);
    }

    // The browser may be closed without finishing, so the wait can be cancelled (SignInCancelCommand).
    [RelayCommand(CanExecute = nameof(CanSignIn), IncludeCancelCommand = true)]
    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;

        try
        {
            User = await _signIn.SignInAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = "Logowanie zostało przerwane.";
            return;
        }
#pragma warning disable CA1031 // Any sign-in failure (network, server, browser) is shown to the user instead of crashing the app.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            ErrorMessage = "Nie udało się zalogować: " + exception.Message;
            return;
        }

        CurrentItem = HomeItem;
        Home.UserName = User.Name;
        await Home.LoadAsync(cancellationToken);
        await Employees.LoadAsync(cancellationToken);
        await Departments.LoadAsync(cancellationToken);
        await Positions.LoadAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(IsSignedIn))]
    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await _signIn.SignOutAsync(cancellationToken);
        User = null;
    }

    private bool CanSignIn() => !IsSignedIn;
}
