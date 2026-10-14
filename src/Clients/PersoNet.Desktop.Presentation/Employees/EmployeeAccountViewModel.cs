using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.Contracts.Account;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Employees;

/// <summary>
/// "Konto" tab of an employee's card: access to the employee portal. HR creates the account and gets a one-time link
/// for the employee to set the password (a later link lets them choose a new one), and can block signing in.
/// The account is the one with the employee's work e-mail.
/// </summary>
public sealed partial class EmployeeAccountViewModel(IEmployeesApi api) : EditorViewModel
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private EmployeeResponse? _employee;
    private int _loadVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StateText), nameof(StateBadge), nameof(LinkButtonText), nameof(HasEmail), nameof(CanBlock), nameof(IsBlocked), nameof(HelpText))]
    [NotifyCanExecuteChangedFor(nameof(CreateLinkCommand), nameof(BlockCommand), nameof(UnblockCommand))]
    public partial EmployeeAccountResponse? Account { get; set; }

    /// <summary>The last link given out, to pass on to the employee; null until HR asks for one.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasLink), nameof(LinkText), nameof(LinkHelp))]
    public partial AccountLinkResponse? Link { get; set; }

    /// <summary>First click on "Zablokuj" asks for confirmation; the second one blocks.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BlockText))]
    public partial bool IsConfirmingBlock { get; set; }

    public bool HasEmail => Account?.Email is not null;

    public bool HasLink => Link is not null;

    public bool IsBlocked => Account?.State == AccountState.Blocked;

    public bool CanBlock => Account?.State is AccountState.Invited or AccountState.Active;

    public string LinkText => Link?.Link.AbsoluteUri ?? string.Empty;

    public string LinkHelp => Link is { } link
        ? $"Przekaż ten link pracownikowi. Zadziała raz, do {link.ExpiresAt.ToLocalTime().ToString("d MMMM yyyy, HH:mm", Polish)}."
        : string.Empty;

    public string BlockText => IsConfirmingBlock ? "Na pewno zablokować?" : "Zablokuj konto";

    public string StateBadge => Account?.State switch
    {
        AccountState.Invited => "Czeka na hasło",
        AccountState.Active => "Aktywne",
        AccountState.Blocked => "Zablokowane",
        _ => "Brak konta",
    };

    public string StateText => Account switch
    {
        null => string.Empty,
        { Email: null } => "Pracownik nie ma w kartotece służbowego e-maila, więc nie może dostać konta.",
        { State: AccountState.None } => $"Pracownik nie ma jeszcze konta w portalu. Będzie logować się adresem {Account.Email}.",
        { State: AccountState.Invited } => $"Konto {Account.Email} jest założone, ale pracownik nie ustawił jeszcze hasła.",
        { State: AccountState.Active } => $"Pracownik loguje się do portalu adresem {Account.Email}.",
        _ => $"Konto {Account.Email} jest zablokowane: pracownik nie może się zalogować.",
    };

    public string HelpText => Account switch
    {
        { Email: null } => "Wpisz e-mail w zakładce Dane i zapisz kartotekę.",
        { State: AccountState.None } => "Kliknij „Utwórz konto”, a dostaniesz link, którym pracownik ustawi swoje hasło.",
        { State: AccountState.Invited } => "Jeśli link zginął albo wygasł, wygeneruj nowy. Poprzedni przestanie działać.",
        { State: AccountState.Active } => "Gdy pracownik zapomni hasła, wygeneruj link do ustawienia nowego.",
        _ => string.Empty,
    };

    public string LinkButtonText => Account?.State == AccountState.None ? "Utwórz konto" : "Nowy link do hasła";

    /// <summary>Shows the account of the employee (nothing for a card that is not saved yet).</summary>
    public async Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        var version = ++_loadVersion;

        Account = null;
        Link = null;
        IsConfirmingBlock = false;

        if (employee is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var account = await api.GetAccountAsync(employee.Id, cancellationToken);
            if (version == _loadVersion)
            {
                Account = account;
            }
        });
    }

    [RelayCommand(CanExecute = nameof(CanCreateLink))]
    public Task CreateLinkAsync(CancellationToken cancellationToken)
    {
        IsConfirmingBlock = false;
        return _employee is not { } employee
            ? Task.CompletedTask
            : RunAsync(async () =>
            {
                var link = await api.CreateAccountLinkAsync(employee.Id, cancellationToken);
                Account = link.Account;
                Link = link;
            });
    }

    [RelayCommand(CanExecute = nameof(CanBlock))]
    public Task BlockAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee)
        {
            return Task.CompletedTask;
        }

        if (!IsConfirmingBlock)
        {
            IsConfirmingBlock = true;
            return Task.CompletedTask;
        }

        IsConfirmingBlock = false;
        return RunAsync(async () =>
        {
            Account = await api.SetAccountBlockedAsync(employee.Id, blocked: true, cancellationToken);
            Link = null;
        });
    }

    [RelayCommand(CanExecute = nameof(IsBlocked))]
    public Task UnblockAsync(CancellationToken cancellationToken) => _employee is not { } employee
        ? Task.CompletedTask
        : RunAsync(async () => Account = await api.SetAccountBlockedAsync(employee.Id, blocked: false, cancellationToken));

    protected override bool TryShowOnField(ApiException exception) => false;

    private bool CanCreateLink() => Account is { Email: not null, State: not AccountState.Blocked };
}
