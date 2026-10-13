using Microsoft.Extensions.Time.Testing;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Leave;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class LeaveViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeLeaveApi _leave = new();
    private readonly FakeEmployeesApi _employees = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 5, 20, 9, 0, 0, TimeSpan.Zero));

    private LeaveViewModel CreateViewModel() => new(_leave, _employees, _time);

    [Fact]
    public async Task Opens_on_requests_waiting_for_a_decision_and_counts_them()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        _leave.Add(employee.Id, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6));
        _leave.Add(employee.Id, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), LeaveState.Approved);
        var viewModel = CreateViewModel();

        await viewModel.LoadAsync(Cancellation);

        viewModel.Year.ShouldBe(2025);
        viewModel.Items.ShouldHaveSingleItem().IsPending.ShouldBeTrue();
        viewModel.PendingCount.ShouldBe(1);
        viewModel.Form.Employees.Count.ShouldBe(2);

        viewModel.StateFilter = LeaveViewModel.States.Single(choice => choice.State is null);
        await Task.Yield();
        viewModel.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Rejecting_needs_a_reason_and_approving_moves_the_request_out_of_the_waiting_list()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        var first = _leave.Add(employee.Id, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6));
        var second = _leave.Add(employee.Id, new DateOnly(2025, 7, 7), new DateOnly(2025, 7, 8));
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        viewModel.Selected = viewModel.Items.Single(row => row.Request.Id == first.Id);
        await viewModel.RejectAsync(Cancellation);
        viewModel.GetError(nameof(viewModel.DecisionNote)).ShouldNotBeNull();

        await viewModel.ApproveAsync(Cancellation);

        viewModel.Items.ShouldHaveSingleItem().Request.Id.ShouldBe(second.Id);
        viewModel.Selected!.Request.State.ShouldBe(LeaveState.Approved);
        viewModel.PendingCount.ShouldBe(1);
        viewModel.ApproveCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public async Task New_request_is_saved_and_shown_and_server_errors_land_on_the_right_field()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        viewModel.New();
        await viewModel.SaveAsync(Cancellation);
        viewModel.Form.GetError(nameof(viewModel.Form.EditEmployee)).ShouldBe("Wybierz pracownika.");

        viewModel.Form.EditEmployee = viewModel.Form.Employees.Single(choice => choice.Id == employee.Id);
        viewModel.Form.EditStartDate = new DateTime(2025, 6, 2);
        viewModel.Form.EditEndDate.ShouldBe(new DateTime(2025, 6, 2));
        viewModel.Form.EditEndDate = new DateTime(2025, 6, 6);
        await viewModel.SaveAsync(Cancellation);

        viewModel.Form.GetError(nameof(viewModel.Form.EditKind)).ShouldBe("Najpierw ustaw wymiar urlopu pracownika na ten rok.");
        viewModel.IsCreating.ShouldBeTrue();

        viewModel.Form.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.Unpaid);
        await viewModel.SaveAsync(Cancellation);

        viewModel.IsCreating.ShouldBeFalse();
        viewModel.Selected!.KindName.ShouldBe("Urlop bezpłatny");
        viewModel.Selected.DaysText.ShouldBe("5 dni");
        viewModel.Selected.Period.ShouldBe("2 cze – 6 cze 2025");
    }

    [Fact]
    public async Task Employee_card_sets_the_entitlement_and_shows_the_balance()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        var viewModel = new EmployeeLeaveViewModel(_leave, _time);
        await viewModel.ShowAsync(employee, Cancellation);

        viewModel.HasEntitlement.ShouldBeFalse();
        viewModel.EditDays = "dużo";
        await viewModel.SaveEntitlementAsync(Cancellation);
        viewModel.GetError(nameof(viewModel.EditDays)).ShouldNotBeNull();

        viewModel.EditDays = "26";
        viewModel.EditCarriedOver = "3";
        await viewModel.SaveEntitlementAsync(Cancellation);

        viewModel.HasEntitlement.ShouldBeTrue();
        viewModel.Balance!.RemainingHours.ShouldBe(232m);
        viewModel.RemainingText.ShouldBe("29 dni");
        _leave.Entitlements[(employee.Id, 2025)].Days.ShouldBe(26);
    }

    [Fact]
    public async Task Leave_in_hours_is_one_day_and_only_for_kinds_that_allow_it()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);
        viewModel.New();
        viewModel.Form.EditEmployee = viewModel.Form.Employees.Single(choice => choice.Id == employee.Id);

        viewModel.Form.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.OnDemand);
        viewModel.Form.CanUseHours.ShouldBeFalse();

        viewModel.Form.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.ChildCare);
        viewModel.Form.IsHourly = true;
        viewModel.Form.EditStartDate = new DateTime(2025, 6, 2);
        viewModel.Form.EditEndDate.ShouldBe(new DateTime(2025, 6, 2));

        viewModel.Form.EditHours = "dwie";
        await viewModel.SaveAsync(Cancellation);
        viewModel.Form.GetError(nameof(viewModel.Form.EditHours)).ShouldNotBeNull();

        viewModel.Form.EditHours = "9";
        await viewModel.SaveAsync(Cancellation);
        viewModel.Form.GetError(nameof(viewModel.Form.EditHours)).ShouldNotBeNull();
        viewModel.IsCreating.ShouldBeTrue();

        viewModel.Form.EditHours = "2:30";
        await viewModel.SaveAsync(Cancellation);

        viewModel.Selected!.Request.IsHourly.ShouldBeTrue();
        viewModel.Selected.Request.Hours.ShouldBe(2.5m);
        viewModel.Selected.DaysText.ShouldBe("2,5 godz.");

        viewModel.New();
        viewModel.Form.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.ChildCare);
        viewModel.Form.IsHourly = true;
        viewModel.Form.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.Unpaid);
        viewModel.Form.IsHourly.ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, "0 dni")]
    [InlineData(4, "4 godz.")]
    [InlineData(8, "1 dzień")]
    [InlineData(108, "13 dni 4 godz.")]
    [InlineData(2.5, "2,5 godz.")]
    [InlineData(-3, "0 dni")]
    public void Leave_time_reads_in_days_of_8_hours(double hours, string text) => LeaveTexts.Amount((decimal)hours).ShouldBe(text);

    [Theory]
    [InlineData("2", 2)]
    [InlineData("2,5", 2.5)]
    [InlineData("2.25", 2.25)]
    [InlineData("1:45", 1.75)]
    public void Hours_can_be_typed_in_common_ways(string text, double hours) => LeaveTexts.ParseHours(text).ShouldBe((decimal)hours);
}
