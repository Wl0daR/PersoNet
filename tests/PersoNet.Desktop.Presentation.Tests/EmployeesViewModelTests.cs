using Microsoft.Extensions.Time.Testing;
using PersoNet.Desktop.Presentation.Employees;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class EmployeesViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeEmployeesApi _employees = new();
    private readonly FakeOrganizationApi _organization = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 5, 20, 9, 0, 0, TimeSpan.Zero));

    private EmployeesViewModel CreateViewModel() => new(_employees, _organization, _time);

    [Fact]
    public async Task New_card_saves_an_employee_who_then_appears_selected_in_the_list()
    {
        var it = _organization.Add("Dział IT", "IT");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        viewModel.New();
        viewModel.EditNumber = "p001";
        viewModel.EditFirstName = "Jan";
        viewModel.EditLastName = "Nowak";
        viewModel.EditDepartment = viewModel.Departments.Single(choice => choice.Id == it.Id);
        await viewModel.SaveAsync(Cancellation);

        viewModel.ErrorMessage.ShouldBeNull();
        viewModel.IsNew.ShouldBeFalse();
        viewModel.Items.ShouldHaveSingleItem().Number.ShouldBe("P001");
        viewModel.Selected!.Id.ShouldBe(viewModel.Current!.Id);
        _employees.Employees.Single().DepartmentId.ShouldBe(it.Id);
        _employees.Employees.Single().HireDate.ShouldBe(new DateOnly(2025, 5, 20));
    }

    [Fact]
    public async Task Missing_required_fields_and_bad_email_are_flagged_without_calling_the_api()
    {
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        viewModel.EditEmail = "jan.nowak";
        await viewModel.SaveAsync(Cancellation);

        _employees.Employees.ShouldBeEmpty();
        viewModel.GetError(nameof(viewModel.EditNumber)).ShouldBe("Podaj numer ewidencyjny.");
        viewModel.GetError(nameof(viewModel.EditFirstName)).ShouldNotBeNull();
        viewModel.GetError(nameof(viewModel.EditLastName)).ShouldNotBeNull();
        viewModel.GetError(nameof(viewModel.EditEmail)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Taken_number_is_shown_under_the_number_field()
    {
        _employees.Add("P001", "Anna", "Zielińska");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        viewModel.New();
        viewModel.EditNumber = "P001";
        viewModel.EditFirstName = "Jan";
        viewModel.EditLastName = "Nowak";
        await viewModel.SaveAsync(Cancellation);

        viewModel.GetError(nameof(viewModel.EditNumber)).ShouldBe("Pracownik o tym numerze ewidencyjnym już istnieje.");
    }

    [Fact]
    public async Task Opened_employee_is_not_offered_as_their_own_manager()
    {
        var anna = _employees.Add("P001", "Anna", "Zielińska");
        var jan = _employees.Add("P002", "Jan", "Nowak");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        await viewModel.OpenAsync(viewModel.Items.Single(item => item.Id == jan.Id), Cancellation);

        viewModel.EditFirstName.ShouldBe("Jan");
        viewModel.Managers.Select(choice => choice.Id).ShouldBe([null, anna.Id]);
    }

    [Fact]
    public async Task Terminate_hides_the_employee_unless_former_employees_are_shown_and_reinstate_undoes_it()
    {
        var jan = _employees.Add("P002", "Jan", "Nowak");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);
        await viewModel.OpenAsync(viewModel.Items.Single(), Cancellation);

        viewModel.TerminationLastDay = new DateTime(2025, 5, 31);
        await viewModel.TerminateAsync(Cancellation);

        viewModel.IsTerminated.ShouldBeTrue();
        viewModel.StatusText.ShouldContain("31 maja 2025");
        viewModel.Items.ShouldBeEmpty();
        viewModel.EmptyTitle.ShouldBe("Nie ma jeszcze pracowników");

        viewModel.IncludeFormer = true;
        await viewModel.SearchAsync(Cancellation);
        viewModel.Items.ShouldHaveSingleItem().Id.ShouldBe(jan.Id);

        await viewModel.ReinstateAsync(Cancellation);
        viewModel.IsEmployed.ShouldBeTrue();
    }

    [Fact]
    public async Task Termination_before_hire_is_shown_under_the_date()
    {
        _employees.Add("P002", "Jan", "Nowak", new DateOnly(2025, 1, 1));
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);
        await viewModel.OpenAsync(viewModel.Items.Single(), Cancellation);

        viewModel.TerminationLastDay = new DateTime(2024, 12, 31);
        await viewModel.TerminateAsync(Cancellation);

        viewModel.GetError(nameof(viewModel.TerminationLastDay)).ShouldNotBeNull();
        viewModel.IsEmployed.ShouldBeTrue();
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
}
