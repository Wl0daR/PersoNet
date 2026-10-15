using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Company;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class CompanyViewModelTests
{
    private readonly FakeCompanyApi _api = new();
    private readonly FakeDialogService _dialogs = new();

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Shows_missing_data_as_not_given_and_every_kind_of_leave_in_the_portal()
    {
        var screen = new CompanyViewModel(_api, _dialogs);

        await screen.LoadAsync(Cancellation);

        screen.Name.ShouldBe("Acme");
        screen.Details.Single(fact => fact.Label == "NIP").IsGiven.ShouldBeFalse();
        screen.LeaveKinds.ShouldAllBe(row => row.InPortal);
        screen.PortalSummary.ShouldBe("Pracownicy proszą w portalu o każdy rodzaj urlopu.");
    }

    [Fact]
    public async Task Data_changed_in_the_dialog_is_shown_on_the_screen()
    {
        var screen = new CompanyViewModel(_api, _dialogs);
        await screen.LoadAsync(Cancellation);
        _dialogs.When<CompanyDetailsDialogViewModel>(async dialog =>
        {
            dialog.EditLegalName = "Acme Polska sp. z o.o.";
            dialog.EditTaxId = "526 104 38 38";
            dialog.EditStreet = "ul. Prosta 1";
            dialog.EditPostalCode = "00-850";
            dialog.EditCity = "Warszawa";
            await dialog.SaveAsync(Cancellation);
        });

        await screen.EditDetailsAsync();

        screen.Details.Single(fact => fact.Label == "NIP").Value.ShouldBe("526-104-38-38");
        screen.Details.Single(fact => fact.Label == "Adres").Value.ShouldBe("ul. Prosta 1, 00-850 Warszawa");
        screen.Details.Single(fact => fact.Label == "Pełna nazwa").Value.ShouldBe("Acme Polska sp. z o.o.");
    }

    [Fact]
    public async Task Dialog_shows_errors_at_the_fields_and_stays_open()
    {
        var dialog = new CompanyDetailsDialogViewModel(_api, _api.Company);
        var closed = false;
        dialog.CloseRequested += (_, _) => closed = true;

        dialog.EditName = " ";
        dialog.EditPostalCode = "00850";
        dialog.EditTaxId = "123";
        await dialog.SaveAsync(Cancellation);

        dialog.GetError(nameof(dialog.EditName)).ShouldBe("Podaj nazwę firmy.");
        dialog.GetError(nameof(dialog.EditPostalCode)).ShouldBe("Wpisz kod w formie 00-000.");
        dialog.GetError(nameof(dialog.EditTaxId)).ShouldBe("NIP ma 10 cyfr.");

        dialog.EditName = "Acme";
        dialog.EditPostalCode = "00-850";
        dialog.EditTaxId = "5261043839";
        await dialog.SaveAsync(Cancellation);

        dialog.GetError(nameof(dialog.EditTaxId)).ShouldBe("NIP jest niepoprawny.");
        closed.ShouldBeFalse();
    }

    [Fact]
    public async Task Kinds_unchecked_in_the_dialog_are_left_to_hr()
    {
        var screen = new CompanyViewModel(_api, _dialogs);
        await screen.LoadAsync(Cancellation);
        _dialogs.When<LeaveSettingsDialogViewModel>(async dialog =>
        {
            foreach (var option in dialog.Options.Where(option => option.Kind is not (LeaveKind.Annual or LeaveKind.OnDemand)))
            {
                option.IsChecked = false;
            }

            await dialog.SaveAsync(Cancellation);
        });

        await screen.EditLeaveAsync();

        _api.Company.PortalLeaveKinds.ShouldBe([LeaveKind.Annual, LeaveKind.OnDemand]);
        screen.LeaveKinds.Where(row => row.InPortal).Select(row => row.Name).ShouldBe(["Urlop wypoczynkowy", "Urlop na żądanie"]);
        screen.PortalSummary.ShouldBe("W portalu: 2 z 13 rodzajów. Pozostałe wpisuje dział kadr.");
    }
}
