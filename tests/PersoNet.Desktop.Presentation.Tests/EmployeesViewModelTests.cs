using Microsoft.Extensions.Time.Testing;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Employees;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class EmployeesViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeEmployeesApi _employees = new();
    private readonly FakeOrganizationApi _organization = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 5, 20, 9, 0, 0, TimeSpan.Zero));

    private EmployeesViewModel CreateViewModel() => new(_employees, _organization, new FakeLeaveApi(), new FakeAccessApi(), new FakeWorkTimeApi(), new FakeFileService(), UserAccessFor.Administrator(), _time, _dialogs);

    private async Task<EmployeeCardViewModel> OpenCardAsync(Guid? employeeId)
    {
        var card = CreateViewModel().CreateCard();
        await card.LoadAsync(employeeId, Cancellation);
        return card;
    }

    [Fact]
    public async Task New_card_saves_an_employee_who_then_appears_selected_in_the_list()
    {
        var it = _organization.Add("Dział IT", "IT");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        EmployeeCardViewModel? card = null;
        _dialogs.When<EmployeeCardViewModel>(async opened =>
        {
            card = opened;
            opened.EditNumber = "p001";
            opened.EditFirstName = "Jan";
            opened.EditLastName = "Nowak";
            opened.EditDepartment = opened.Departments.Single(choice => choice.Id == it.Id);
            await opened.SaveAsync(Cancellation);
        });
        await viewModel.NewAsync(Cancellation);

        card!.ErrorMessage.ShouldBeNull();
        card.IsNew.ShouldBeFalse();
        card.Title.ShouldBe("Kartoteka pracownika – Jan Nowak");
        card.SavedMessage.ShouldBe("Kartoteka założona. Możesz teraz dodać umowy i urlop.");
        viewModel.Items.ShouldHaveSingleItem().Number.ShouldBe("P001");
        viewModel.Selected!.Id.ShouldBe(card.Current!.Id);
        _employees.Employees.Single().DepartmentId.ShouldBe(it.Id);
        _employees.Employees.Single().HireDate.ShouldBe(new DateOnly(2025, 5, 20));
    }

    [Fact]
    public async Task Picking_a_department_lists_its_positions_and_the_general_ones()
    {
        var production = _organization.Add("Produkcja", "PRO");
        var paintShop = _organization.Add("Lakiernia", "LAK", production.Id);
        var it = _organization.Add("Dział IT", "IT");
        _organization.AddPosition("Programista", it.Id);
        _organization.AddPosition("Lakiernik", paintShop.Id);
        _organization.AddPosition("Kierownik produkcji", production.Id);
        _organization.AddPosition("Praktykant");
        var card = await OpenCardAsync(employeeId: null);

        card.CanFilterPositions.ShouldBeFalse();
        card.Positions.Count.ShouldBe(5);

        // A department's positions include those of the departments above it, and the ones tied to no department.
        card.EditDepartment = card.Departments.Single(choice => choice.Id == paintShop.Id);
        card.CanFilterPositions.ShouldBeTrue();
        card.Positions.Select(choice => choice.Name).ShouldBe(["(bez stanowiska)", "Lakiernik", "Kierownik produkcji", "Praktykant"]);

        card.EditPosition = card.Positions.Single(choice => choice.Name == "Lakiernik");
        card.EditDepartment = card.Departments.Single(choice => choice.Id == it.Id);
        card.Positions.Select(choice => choice.Name).ShouldBe(["(bez stanowiska)", "Programista", "Lakiernik", "Praktykant"]);
        card.EditPosition.Name.ShouldBe("Lakiernik");

        card.ShowAllPositions = true;
        card.Positions.Count.ShouldBe(5);
    }

    [Fact]
    public async Task Missing_required_fields_and_bad_email_are_flagged_without_calling_the_api()
    {
        var card = await OpenCardAsync(employeeId: null);

        card.EditEmail = "jan.nowak";
        await card.SaveAsync(Cancellation);

        _employees.Employees.ShouldBeEmpty();
        card.GetError(nameof(card.EditNumber)).ShouldBe("Podaj numer ewidencyjny.");
        card.GetError(nameof(card.EditFirstName)).ShouldNotBeNull();
        card.GetError(nameof(card.EditLastName)).ShouldNotBeNull();
        card.GetError(nameof(card.EditEmail)).ShouldNotBeNull();
        card.SavedMessage.ShouldBeNull();
    }

    [Fact]
    public async Task Taken_number_is_shown_under_the_number_field()
    {
        _employees.Add("P001", "Anna", "Zielińska");
        var card = await OpenCardAsync(employeeId: null);

        card.EditNumber = "P001";
        card.EditFirstName = "Jan";
        card.EditLastName = "Nowak";
        await card.SaveAsync(Cancellation);

        card.GetError(nameof(card.EditNumber)).ShouldBe("Pracownik o tym numerze ewidencyjnym już istnieje.");
    }

    [Fact]
    public async Task Opened_employee_is_not_offered_as_their_own_manager()
    {
        var anna = _employees.Add("P001", "Anna", "Zielińska");
        var jan = _employees.Add("P002", "Jan", "Nowak");

        var card = await OpenCardAsync(jan.Id);

        card.EditFirstName.ShouldBe("Jan");
        card.Managers.Select(choice => choice.Id).ShouldBe([null, anna.Id]);
    }

    [Fact]
    public async Task Ending_employment_in_the_card_hides_the_employee_from_the_list_and_reinstating_brings_them_back()
    {
        var jan = _employees.Add("P002", "Jan", "Nowak");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);
        viewModel.Selected = viewModel.Items.Single();

        EmployeeCardViewModel? card = null;
        _dialogs.When<EmployeeCardViewModel>(async opened =>
        {
            card = opened;
            opened.TerminationLastDay = new DateTime(2025, 5, 31);
            await opened.TerminateAsync(Cancellation);
        });
        await viewModel.OpenAsync(null, Cancellation);

        card!.IsTerminated.ShouldBeTrue();
        card.StatusText.ShouldContain("31 maja 2025");
        viewModel.Items.ShouldBeEmpty();
        viewModel.EmptyTitle.ShouldBe("Nie ma jeszcze pracowników");

        viewModel.IncludeFormer = true;
        await viewModel.SearchAsync(Cancellation);
        viewModel.Items.ShouldHaveSingleItem().Id.ShouldBe(jan.Id);

        _dialogs.When<EmployeeCardViewModel>(opened => opened.ReinstateAsync(Cancellation));
        await viewModel.OpenAsync(viewModel.Items.Single(), Cancellation);
        _employees.Employees.Single().TerminationDate.ShouldBeNull();
    }

    [Fact]
    public async Task Termination_before_hire_is_shown_under_the_date()
    {
        var jan = _employees.Add("P002", "Jan", "Nowak", new DateOnly(2025, 1, 1));
        var card = await OpenCardAsync(jan.Id);

        card.TerminationLastDay = new DateTime(2024, 12, 31);
        await card.TerminateAsync(Cancellation);

        card.GetError(nameof(card.TerminationLastDay)).ShouldNotBeNull();
        card.IsEmployed.ShouldBeTrue();
    }

    [Fact]
    public async Task Pages_move_forward_and_back_and_show_a_polish_count()
    {
        for (var i = 0; i < EmployeesViewModel.PageSize + 2; i++)
        {
            _employees.Add($"P{i:000}", "Osoba", $"Nazwisko{i:000}");
        }

        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        viewModel.PageInfo.ShouldBe("Strona 1 z 2 · 52 osoby");
        viewModel.PreviousPageCommand.CanExecute(null).ShouldBeFalse();

        await viewModel.NextPageAsync(Cancellation);

        viewModel.Page.ShouldBe(2);
        viewModel.Items.Count.ShouldBe(2);
        viewModel.NextPageCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task Picked_person_opens_in_a_card_window_and_closing_it_without_changes_keeps_the_list()
    {
        _employees.Add("P001", "Anna", "Zielińska");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);
        var queries = _employees.Queries.Count;

        viewModel.OpenCommand.CanExecute(null).ShouldBeFalse();
        viewModel.Selected = viewModel.Items.Single();
        viewModel.OpenCommand.CanExecute(null).ShouldBeTrue();

        await viewModel.OpenAsync(null, Cancellation);

        var card = _dialogs.Shown.OfType<EmployeeCardViewModel>().ShouldHaveSingleItem();
        card.Title.ShouldBe("Kartoteka pracownika – Anna Zielińska");
        card.CardInitials.ShouldBe("AZ");
        card.CardSubtitle.ShouldBe("P001");
        card.StatusBadge.ShouldBe("Zatrudniony");
        _employees.Queries.Count.ShouldBe(queries);

        await viewModel.NewAsync(Cancellation);
        var empty = _dialogs.Shown.OfType<EmployeeCardViewModel>().Last();
        empty.Title.ShouldBe("Nowy pracownik");
        empty.StatusBadge.ShouldBe("Nowa kartoteka");
    }

    [Fact]
    public async Task Clicking_a_column_sorts_the_whole_list_and_clicking_it_again_reverses_the_order()
    {
        _employees.Add("P001", "Anna", "Zielińska");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        await viewModel.SortByAsync(EmployeeSort.HireDate, Cancellation);
        _employees.Queries[^1].Sort.ShouldBe(EmployeeSort.HireDate);
        _employees.Queries[^1].Descending.ShouldBeFalse();
        _employees.Queries[^1].Page.ShouldBe(1);

        await viewModel.SortByAsync(EmployeeSort.HireDate, Cancellation);
        _employees.Queries[^1].Descending.ShouldBeTrue();

        await viewModel.SortByAsync(EmployeeSort.Department, Cancellation);
        _employees.Queries[^1].Sort.ShouldBe(EmployeeSort.Department);
        _employees.Queries[^1].Descending.ShouldBeFalse();
    }

    [Fact]
    public async Task Former_employee_is_rehired_from_the_card_on_a_later_date()
    {
        var jan = _employees.Add("P002", "Jan", "Nowak", new DateOnly(2024, 1, 1));
        var card = await OpenCardAsync(jan.Id);
        card.TerminationLastDay = new DateTime(2025, 3, 31);
        await card.TerminateAsync(Cancellation);

        card.RehireCommand.CanExecute(null).ShouldBeTrue();
        card.RehireDate = new DateTime(2025, 5, 1);
        await card.RehireAsync(Cancellation);

        card.ErrorMessage.ShouldBeNull();
        card.IsEmployed.ShouldBeTrue();
        card.SavedMessage.ShouldBe("Pracownik zatrudniony ponownie. Dodaj nową umowę w zakładce Umowy.");
        _employees.Employees.Single().HireDate.ShouldBe(new DateOnly(2025, 5, 1));
        _employees.Employees.Single().TerminationDate.ShouldBeNull();
    }

    [Fact]
    public async Task Typing_in_the_search_box_searches_once_after_a_short_pause()
    {
        _employees.Add("P001", "Anna", "Zielińska");
        _employees.Add("P002", "Jan", "Nowak");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);
        var queriesBefore = _employees.Queries.Count;

        viewModel.Search = "n";
        viewModel.Search = "no";
        viewModel.Search = "now";
        _employees.Queries.Count.ShouldBe(queriesBefore);

        _time.Advance(EmployeesViewModel.SearchDelay);
        await Task.Yield();

        _employees.Queries.Count.ShouldBe(queriesBefore + 1);
        _employees.Queries[^1].Search.ShouldBe("now");
        viewModel.Items.ShouldHaveSingleItem().LastName.ShouldBe("Nowak");
    }
}
