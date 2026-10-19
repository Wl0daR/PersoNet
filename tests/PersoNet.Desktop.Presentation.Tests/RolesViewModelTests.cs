using PersoNet.Desktop.Presentation.Access;
using PersoNet.Desktop.Presentation.Employees;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class RolesViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeAccessApi _api = new();
    private readonly FakeDialogService _dialogs = new();

    [Fact]
    public async Task New_role_is_set_up_in_a_window_with_permissions_grouped_by_area()
    {
        var roles = new RolesViewModel(_api, _dialogs);
        await roles.LoadAsync(Cancellation);
        roles.IsEmpty.ShouldBeTrue();

        RoleDialogViewModel? dialog = null;
        _dialogs.When<RoleDialogViewModel>(async opened =>
        {
            dialog = opened;
            opened.EditName = "Kierownik działu";
            opened.Groups.Select(group => group.Name).ShouldBe(["Pracownicy", "Urlopy", "Firma"]);
            var accounts = opened.Groups.SelectMany(group => group.Items).Single(option => option.Code == "accounts.manage");
            accounts.IsEnabled.ShouldBeFalse();
            foreach (var option in opened.Groups.Single(group => group.Name == "Urlopy").Items)
            {
                option.IsChecked = true;
            }

            await opened.SaveAsync(Cancellation);
        });
        await roles.NewAsync(Cancellation);

        dialog!.Title.ShouldBe("Nowa rola");
        var row = roles.Items.ShouldHaveSingleItem();
        roles.Selected.ShouldBe(row);
        row.Name.ShouldBe("Kierownik działu");
        row.PermissionsText.ShouldBe("Decyzje urlopowe, Przeglądanie urlopów");
        row.MembersText.ShouldBe("nikt");
    }

    [Fact]
    public async Task Taken_name_is_shown_under_the_name_and_an_empty_name_is_caught_before_saving()
    {
        _api.Add("Kadrowa", "employees.read");
        var dialog = new RoleDialogViewModel(_api, FakeAccessApi.Catalog, editing: null);

        await dialog.SaveAsync(Cancellation);
        dialog.GetError(nameof(dialog.EditName)).ShouldBe("Podaj nazwę roli.");

        dialog.EditName = "kadrowa";
        await dialog.SaveAsync(Cancellation);
        dialog.GetError(nameof(dialog.EditName)).ShouldBe("Rola o tej nazwie już istnieje.");
        _api.Roles.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Removing_needs_a_second_click_and_a_role_people_have_stays_with_a_message()
    {
        var role = _api.Add("Kadrowa", "employees.read");
        _api.Assignments[Guid.NewGuid()] = [role.Id];
        var roles = new RolesViewModel(_api, _dialogs);
        await roles.LoadAsync(Cancellation);
        roles.Selected = roles.Items.Single();
        roles.Selected.MembersText.ShouldBe("1 osoba");

        await roles.DeleteAsync(Cancellation);
        roles.DeleteText.ShouldBe("Na pewno usunąć?");
        await roles.DeleteAsync(Cancellation);

        roles.ErrorMessage.ShouldBe("Tę rolę mają pracownicy.");
        _api.Roles.ShouldHaveSingleItem();

        _api.Assignments.Clear();
        await roles.DeleteAsync(Cancellation);
        await roles.DeleteAsync(Cancellation);
        roles.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Employee_roles_are_checked_in_the_card_and_saved_together()
    {
        var employees = new FakeEmployeesApi();
        var jan = employees.Add("P001", "Jan", "Nowak");
        var manager = _api.Add("Kierownik", "leave.manage");
        _api.Add("Kadrowa", "employees.read");
        var roles = new EmployeeRolesViewModel(_api);

        await roles.ShowAsync(jan, Cancellation);
        roles.Options.Select(option => option.Name).ShouldBe(["Kadrowa", "Kierownik"]);
        roles.SaveCommand.CanExecute(null).ShouldBeFalse();

        roles.Options.Single(option => option.Name == "Kierownik").IsChecked = true;
        roles.SaveCommand.CanExecute(null).ShouldBeTrue();
        await roles.SaveAsync(Cancellation);

        _api.Assignments[jan.Id].ShouldBe([manager.Id]);
        roles.HasChanges.ShouldBeFalse();
        roles.SavedMessage.ShouldBe("Zapisano. Pracownik ma teraz uprawnienia zaznaczonych ról.");
    }

    [Fact]
    public async Task Card_of_someone_who_may_only_read_hides_saving_and_the_tabs_they_cannot_use()
    {
        var employees = new FakeEmployeesApi();
        var jan = employees.Add("P001", "Jan", "Nowak");
        var screen = new EmployeesViewModel(
            employees, new FakeOrganizationApi(), new FakeLeaveApi(), new FakeAbsencesApi(), _api, new FakeWorkTimeApi(), new FakeSafetyApi(), new FakeFileService(), UserAccessFor.With(UserAccess.EmployeesRead), TimeProvider.System, _dialogs);
        var card = screen.CreateCard();

        await card.LoadAsync(jan.Id, Cancellation);

        card.CanEdit.ShouldBeFalse();
        card.CanSeeLeave.ShouldBeFalse();
        card.CanSeeAccessTab.ShouldBeFalse();
        card.CanSeeHistory.ShouldBeFalse();
        employees.HistoryReads.ShouldBe(0);
    }
}
