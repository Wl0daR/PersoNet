using Microsoft.Extensions.Time.Testing;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Employees;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class ContractsViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeEmployeesApi _api = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2024, 5, 10, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Probation_then_follow_up_contract_starts_the_day_after()
    {
        var employee = _api.Add("P001", "Jan", "Nowak", new DateOnly(2024, 3, 1));
        var viewModel = new ContractsViewModel(_api, _time);
        await viewModel.ShowAsync(employee, Cancellation);

        viewModel.EditStartDate.ShouldBe(new DateTime(2024, 3, 1));
        viewModel.EditKind = ContractsViewModel.Kinds.Single(kind => kind.Kind == ContractKind.Probation);
        viewModel.EditEndDate = new DateTime(2024, 5, 31);
        await viewModel.SaveAsync(Cancellation);

        var row = viewModel.Items.ShouldHaveSingleItem();
        row.TypeName.ShouldBe("Umowa o pracę na okres próbny");
        row.Period.ShouldBe("1 mar 2024 – 31 maj 2024");
        row.WorkingTimeText.ShouldBe("pełny etat");
        row.IsCurrent.ShouldBeTrue();

        viewModel.New();

        viewModel.EditStartDate.ShouldBe(new DateTime(2024, 6, 1));
    }

    [Fact]
    public async Task Fixed_term_without_end_and_bad_working_time_are_flagged_without_calling_the_api()
    {
        var employee = _api.Add("P001", "Jan", "Nowak");
        var viewModel = new ContractsViewModel(_api, _time);
        await viewModel.ShowAsync(employee, Cancellation);

        viewModel.EditKind = ContractsViewModel.Kinds.Single(kind => kind.Kind == ContractKind.FixedTerm);
        viewModel.EditWorkingTime = "pół";
        await viewModel.SaveAsync(Cancellation);

        _api.Contracts.ShouldBeEmpty();
        viewModel.GetError(nameof(viewModel.EditEndDate)).ShouldBe("Podaj datę końca umowy.");
        viewModel.GetError(nameof(viewModel.EditWorkingTime)).ShouldNotBeNull();
    }

    [Theory]
    [InlineData("1", 1.0)]
    [InlineData("1/2", 0.5)]
    [InlineData("0,75", 0.75)]
    [InlineData(" 3/4 ", 0.75)]
    public void Working_time_is_read_as_fraction_or_decimal(string text, double expected)
    {
        ContractsViewModel.ParseFraction(text).ShouldBe((decimal)expected);
    }

    [Fact]
    public async Task Overlap_from_the_server_is_shown_under_the_start_date()
    {
        var employee = _api.Add("P001", "Jan", "Nowak");
        _api.Contracts.Add(new ContractResponse(Guid.CreateVersion7(), employee.Id, ContractKind.Indefinite, new DateOnly(2024, 1, 1), null, 1m));
        var viewModel = new ContractsViewModel(_api, _time);
        await viewModel.ShowAsync(employee, Cancellation);

        viewModel.New();
        viewModel.EditStartDate = new DateTime(2024, 6, 1);
        await viewModel.SaveAsync(Cancellation);

        viewModel.GetError(nameof(viewModel.EditStartDate)).ShouldBe("W tym okresie pracownik ma już inną umowę o pracę.");
    }

    [Fact]
    public async Task Delete_asks_for_a_second_click_before_removing()
    {
        var employee = _api.Add("P001", "Jan", "Nowak");
        _api.Contracts.Add(new ContractResponse(Guid.CreateVersion7(), employee.Id, ContractKind.Mandate, new DateOnly(2024, 1, 1), null, null));
        var viewModel = new ContractsViewModel(_api, _time);
        await viewModel.ShowAsync(employee, Cancellation);
        viewModel.Selected = viewModel.Items.Single();

        await viewModel.DeleteAsync(Cancellation);

        _api.Contracts.Count.ShouldBe(1);
        viewModel.DeleteText.ShouldBe("Na pewno usunąć?");

        await viewModel.DeleteAsync(Cancellation);

        _api.Contracts.ShouldBeEmpty();
        viewModel.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task Opening_an_employee_card_loads_their_contracts()
    {
        var employee = _api.Add("P001", "Jan", "Nowak");
        _api.Contracts.Add(new ContractResponse(Guid.CreateVersion7(), employee.Id, ContractKind.Indefinite, new DateOnly(2024, 1, 1), null, 0.5m));
        var employees = new EmployeesViewModel(_api, new FakeOrganizationApi(), _time);
        await employees.LoadAsync(Cancellation);

        await employees.OpenAsync(employees.Items.Single(), Cancellation);

        employees.Contracts.Items.ShouldHaveSingleItem().WorkingTimeText.ShouldBe("1/2 etatu");
    }
}
