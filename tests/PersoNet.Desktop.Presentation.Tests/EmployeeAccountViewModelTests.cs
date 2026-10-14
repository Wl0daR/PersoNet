using PersoNet.Contracts.Account;
using PersoNet.Desktop.Presentation.Employees;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class EmployeeAccountViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeEmployeesApi _employees = new();

    [Fact]
    public async Task Creating_the_account_shows_the_link_to_pass_on_and_blocking_needs_a_second_click()
    {
        var jan = _employees.Add("P001", "Jan", "Nowak");
        _employees.Employees[0] = jan = jan with { Email = "jan@firma.pl" };
        var account = new EmployeeAccountViewModel(_employees);
        await account.ShowAsync(jan, Cancellation);

        account.StateBadge.ShouldBe("Brak konta");
        account.LinkButtonText.ShouldBe("Utwórz konto");
        account.BlockCommand.CanExecute(null).ShouldBeFalse();

        await account.CreateLinkAsync(Cancellation);

        account.StateBadge.ShouldBe("Czeka na hasło");
        account.HasLink.ShouldBeTrue();
        account.LinkText.ShouldStartWith("https://login.test/account/activate");
        account.LinkHelp.ShouldContain("27 maja 2025");
        account.LinkButtonText.ShouldBe("Nowy link do hasła");

        await account.BlockAsync(Cancellation);
        account.BlockText.ShouldBe("Na pewno zablokować?");
        _employees.Accounts[jan.Id].ShouldBe(AccountState.Invited);

        await account.BlockAsync(Cancellation);
        account.IsBlocked.ShouldBeTrue();
        account.HasLink.ShouldBeFalse();
        account.CreateLinkCommand.CanExecute(null).ShouldBeFalse();

        await account.UnblockAsync(Cancellation);
        account.StateBadge.ShouldBe("Aktywne");
    }

    [Fact]
    public async Task Employee_without_email_is_told_to_enter_one_first()
    {
        var jan = _employees.Add("P001", "Jan", "Nowak");
        var account = new EmployeeAccountViewModel(_employees);

        await account.ShowAsync(jan, Cancellation);

        account.HasEmail.ShouldBeFalse();
        account.CreateLinkCommand.CanExecute(null).ShouldBeFalse();
        account.HelpText.ShouldBe("Wpisz e-mail w zakładce Dane i zapisz kartotekę.");
    }
}
