using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.Desktop.Presentation.Authentication;
using PersoNet.Desktop.Presentation.Employees;
using PersoNet.Desktop.Presentation.Organization;

namespace PersoNet.Desktop.Presentation.Shell;

/// <summary>
/// Main window of the HR desktop app: sign-in, then the module screens.
/// </summary>
public sealed partial class ShellViewModel(
    ISignInService signIn,
    EmployeesViewModel employees,
    DepartmentsViewModel departments,
    PositionsViewModel positions) : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = "PersoNet – Kadry";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSignedIn))]
    [NotifyCanExecuteChangedFor(nameof(SignInCommand), nameof(SignOutCommand))]
    public partial SignedInUser? User { get; set; }

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    public bool IsSignedIn => User is not null;

    public EmployeesViewModel Employees { get; } = employees;

    public DepartmentsViewModel Departments { get; } = departments;

    public PositionsViewModel Positions { get; } = positions;

    // The browser may be closed without finishing, so the wait can be cancelled (SignInCancelCommand).
    [RelayCommand(CanExecute = nameof(CanSignIn), IncludeCancelCommand = true)]
    public async Task SignInAsync(CancellationToken cancellationToken)
    {
        ErrorMessage = null;

        try
        {
            User = await signIn.SignInAsync(cancellationToken);
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

        await Employees.LoadAsync(cancellationToken);
        await Departments.LoadAsync(cancellationToken);
        await Positions.LoadAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(IsSignedIn))]
    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await signIn.SignOutAsync(cancellationToken);
        User = null;
    }

    private bool CanSignIn() => !IsSignedIn;
}
