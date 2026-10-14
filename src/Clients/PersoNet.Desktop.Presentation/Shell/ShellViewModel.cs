using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient.Access;
using PersoNet.Desktop.Presentation.Access;
using PersoNet.Desktop.Presentation.Authentication;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Company;
using PersoNet.Desktop.Presentation.Employees;
using PersoNet.Desktop.Presentation.Home;
using PersoNet.Desktop.Presentation.Leave;
using PersoNet.Desktop.Presentation.Organization;

namespace PersoNet.Desktop.Presentation.Shell;

/// <summary>
/// Main window of the HR desktop app: sign-in, then the module screens picked from the side menu.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly ISignInService _signIn;
    private readonly IThemeService _theme;
    private readonly IAccessApi _access;
    private readonly UserAccess _user;

    public ShellViewModel(
        ISignInService signIn,
        HomeViewModel home,
        EmployeesViewModel employees,
        LeaveViewModel leave,
        DepartmentsViewModel departments,
        PositionsViewModel positions,
        RolesViewModel roles,
        CompanyViewModel company,
        IAccessApi access,
        UserAccess user,
        IThemeService theme)
    {
        _signIn = signIn;
        _theme = theme;
        _access = access;
        _user = user;
        Home = home;
        Employees = employees;
        Leave = leave;
        Departments = departments;
        Positions = positions;
        Roles = roles;
        Company = company;

        HomeItem = new NavigationItem("Start", "\uE80F", home);
        EmployeesItem = new NavigationItem("Pracownicy", "\uE716", employees);
        LeaveItem = new NavigationItem("Urlopy", "\uE787", leave);
        DepartmentsItem = new NavigationItem("Działy", "\uE8B7", departments);
        PositionsItem = new NavigationItem("Stanowiska", "\uE821", positions);
        RolesItem = new NavigationItem("Role", "\uE8D7", roles);
        CompanyItem = new NavigationItem("Ustawienia firmy", "\uE713", company);
        Navigation = [HomeItem];
        CurrentItem = HomeItem;

        Themes =
        [
            new ThemeChoice(AppTheme.System, "Jak w systemie Windows", "\uE770"),
            new ThemeChoice(AppTheme.Light, "Jasny", "\uE706"),
            new ThemeChoice(AppTheme.Dark, "Ciemny", "\uE708"),
        ];
        Theme = Themes.First(choice => choice.Theme == theme.Theme);
        Theme.IsSelected = true;
    }

    /// <summary>Options of the theme menu at the bottom of the side menu.</summary>
    public IReadOnlyList<ThemeChoice> Themes { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ThemeText))]
    public partial ThemeChoice Theme { get; set; }

    /// <summary>Label of the theme button, e.g. "Motyw: Jasny".</summary>
    public string ThemeText => $"Motyw: {Theme.Name}";

    /// <summary>Switches the app to the picked theme at once and keeps it for the next start.</summary>
    [RelayCommand]
    public void ChangeTheme(ThemeChoice? choice)
    {
        if (choice is null || choice == Theme)
        {
            return;
        }

        Theme.IsSelected = false;
        choice.IsSelected = true;
        Theme = choice;
        _theme.SetTheme(choice.Theme);
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

    public LeaveViewModel Leave { get; }

    public DepartmentsViewModel Departments { get; }

    public PositionsViewModel Positions { get; }

    public RolesViewModel Roles { get; }

    public CompanyViewModel Company { get; }

    public NavigationItem HomeItem { get; }

    public NavigationItem EmployeesItem { get; }

    public NavigationItem LeaveItem { get; }

    public NavigationItem DepartmentsItem { get; }

    public NavigationItem PositionsItem { get; }

    public NavigationItem RolesItem { get; }

    public NavigationItem CompanyItem { get; }

    /// <summary>Screens of the side menu the signed-in user has the permissions for.</summary>
    public ObservableCollection<NavigationItem> Navigation { get; }

    /// <summary>The screen picked in the side menu.</summary>
    [ObservableProperty]
    public partial NavigationItem CurrentItem { get; set; }

    [RelayCommand]
    public void Navigate(NavigationItem? item)
    {
        if (item is not null && Navigation.Contains(item))
        {
            CurrentItem = item;
        }
    }

    // Set once the screens have loaded after signing in; from then on opening a screen shows its current data, which may
    // have changed elsewhere (the employee portal, another HR user).
    private bool _screensLoaded;

    partial void OnCurrentItemChanged(NavigationItem oldValue, NavigationItem newValue)
    {
        if (!_screensLoaded || oldValue == newValue)
        {
            return;
        }

        _ = newValue == HomeItem ? Home.LoadAsync(CancellationToken.None) : LoadAsync(newValue, CancellationToken.None);
    }

    /// <summary>Shortcut from the start screen: the card of a new employee.</summary>
    [RelayCommand]
    public void AddEmployee()
    {
        Navigate(EmployeesItem);
        Employees.NewCommand.Execute(null);
    }

    /// <summary>Shortcut from the start screen: the form of a new leave request.</summary>
    [RelayCommand]
    public void AddLeave()
    {
        Navigate(LeaveItem);
        Leave.NewCommand.Execute(null);
    }

    /// <summary>Shortcut from the start screen: an empty department form.</summary>
    [RelayCommand]
    public void AddDepartment()
    {
        Navigate(DepartmentsItem);
        Departments.Selected = null;
        Departments.NewCommand.Execute(null);
    }

    /// <summary>Shortcut from the start screen: an empty position form.</summary>
    [RelayCommand]
    public void AddPosition()
    {
        Navigate(PositionsItem);
        Positions.NewCommand.Execute(null);
    }

    // The browser may be closed without finishing, so the wait can be cancelled (SignInCancelCommand).
    [RelayCommand(CanExecute = nameof(CanSignIn), IncludeCancelCommand = true)]
    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;

        try
        {
            var user = await _signIn.SignInAsync(cancellationToken);
            _user.Set((await _access.GetCurrentUserAsync(cancellationToken)).Permissions);
            User = user;
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

        ShowPermittedScreens();
        CurrentItem = HomeItem;
        Home.UserName = User.Name;
        await Home.LoadAsync(cancellationToken);
        foreach (var item in Navigation.Where(item => item != HomeItem))
        {
            await LoadAsync(item, cancellationToken);
        }

        _screensLoaded = true;
    }

    [RelayCommand(CanExecute = nameof(IsSignedIn))]
    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await _signIn.SignOutAsync(cancellationToken);
        _screensLoaded = false;
        User = null;
        _user.Clear();
        ShowPermittedScreens();
    }

    // The side menu lists only the screens the user may work on; the API checks every call anyway.
    private void ShowPermittedScreens()
    {
        (NavigationItem Item, bool Permitted)[] screens =
        [
            (HomeItem, true),
            (EmployeesItem, _user.Can(UserAccess.EmployeesRead)),
            (LeaveItem, _user.Can(UserAccess.LeaveRead)),
            (DepartmentsItem, _user.Can(UserAccess.OrganizationManage)),
            (PositionsItem, _user.Can(UserAccess.OrganizationManage)),
            (RolesItem, _user.Can(UserAccess.RolesManage)),
            (CompanyItem, _user.Can(UserAccess.SettingsManage)),
        ];

        // Start stays selected while the others come and go, so the menu never loses its selection.
        CurrentItem = HomeItem;
        var position = 0;
        foreach (var (item, permitted) in screens)
        {
            if (!permitted)
            {
                Navigation.Remove(item);
                continue;
            }

            if (!Navigation.Contains(item))
            {
                Navigation.Insert(position, item);
            }

            position++;
        }
    }

    private static Task LoadAsync(NavigationItem item, CancellationToken cancellationToken) => item.Page switch
    {
        EmployeesViewModel employees => employees.LoadAsync(cancellationToken),
        LeaveViewModel leave => leave.LoadAsync(cancellationToken),
        DepartmentsViewModel departments => departments.LoadAsync(cancellationToken),
        PositionsViewModel positions => positions.LoadAsync(cancellationToken),
        RolesViewModel roles => roles.LoadAsync(cancellationToken),
        CompanyViewModel company => company.LoadAsync(cancellationToken),
        _ => Task.CompletedTask,
    };

    private bool CanSignIn() => !IsSignedIn;
}
