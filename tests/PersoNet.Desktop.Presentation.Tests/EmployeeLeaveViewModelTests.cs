using Microsoft.Extensions.Time.Testing;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Leave;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class EmployeeLeaveViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeLeaveApi _leave = new();
    private readonly FakeEmployeesApi _employees = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 5, 20, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task Without_an_entitlement_says_so_and_saving_one_shows_the_balance()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        _leave.Add(employee.Id, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), LeaveState.Approved);
        var viewModel = new EmployeeLeaveViewModel(_leave, _time);

        await viewModel.ShowAsync(await _employees.GetAsync(employee.Id, Cancellation), Cancellation);

        viewModel.Year.ShouldBe(2025);
        viewModel.HasEntitlement.ShouldBeFalse();
        viewModel.Summary.ShouldContain("nie jest jeszcze ustawiony");
        viewModel.Items.ShouldHaveSingleItem();

        viewModel.EditDays = "26";
        viewModel.EditCarriedOver = "4";
        await viewModel.SaveEntitlementAsync(Cancellation);

        viewModel.HasEntitlement.ShouldBeTrue();
        viewModel.Balance!.RemainingHours.ShouldBe(232m);
        viewModel.RemainingText.ShouldBe("29 dni");
        _leave.Entitlements[(employee.Id, 2025)].ShouldBe(new SaveLeaveEntitlementRequest(26, 4));
    }

    [Theory]
    [InlineData("", "0")]
    [InlineData("61", "0")]
    [InlineData("20", "-1")]
    [InlineData("dwadzieścia", "0")]
    public async Task Wrong_numbers_are_shown_on_the_fields_and_not_sent(string days, string carriedOver)
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        var viewModel = new EmployeeLeaveViewModel(_leave, _time);
        await viewModel.ShowAsync(await _employees.GetAsync(employee.Id, Cancellation), Cancellation);

        viewModel.EditDays = days;
        viewModel.EditCarriedOver = carriedOver;
        await viewModel.SaveEntitlementAsync(Cancellation);

        (viewModel.GetError(nameof(viewModel.EditDays)) ?? viewModel.GetError(nameof(viewModel.EditCarriedOver))).ShouldNotBeNull();
        _leave.Entitlements.ShouldBeEmpty();
    }
}
