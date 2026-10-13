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

    private readonly FakeDialogService _dialogs = new();

    private LeaveViewModel CreateViewModel() => new(_leave, _employees, _time, _dialogs);

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

        viewModel.StateFilter = LeaveViewModel.States.Single(choice => choice.State is null);
        await Task.Yield();
        viewModel.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Rejecting_needs_a_reason_and_approving_in_the_request_window_moves_it_out_of_the_waiting_list()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        var first = _leave.Add(employee.Id, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6));
        var second = _leave.Add(employee.Id, new DateOnly(2025, 7, 7), new DateOnly(2025, 7, 8));
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        viewModel.OpenCommand.CanExecute(null).ShouldBeFalse();
        LeaveDecisionViewModel? dialog = null;
        _dialogs.When<LeaveDecisionViewModel>(async opened =>
        {
            dialog = opened;
            await opened.RejectAsync(Cancellation);
            opened.GetError(nameof(opened.DecisionNote)).ShouldNotBeNull();
            opened.HasChanges.ShouldBeFalse();

            await opened.ApproveAsync(Cancellation);
        });
        await viewModel.OpenAsync(viewModel.Items.Single(row => row.Request.Id == first.Id), Cancellation);

        dialog!.Title.ShouldBe("Wniosek urlopowy – Nowak Jan");
        dialog.Row.Request.State.ShouldBe(LeaveState.Approved);
        dialog.CanDecide.ShouldBeFalse();
        viewModel.Items.ShouldHaveSingleItem().Request.Id.ShouldBe(second.Id);
        viewModel.PendingCount.ShouldBe(1);
    }

    [Fact]
    public async Task Closing_the_request_window_without_a_decision_changes_nothing()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        _leave.Add(employee.Id, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6));
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);
        viewModel.Selected = viewModel.Items.Single();

        await viewModel.OpenAsync(null, Cancellation);

        _dialogs.Shown.ShouldHaveSingleItem().ShouldBeOfType<LeaveDecisionViewModel>();
        viewModel.Items.ShouldHaveSingleItem().IsPending.ShouldBeTrue();
    }

    [Fact]
    public async Task New_request_is_saved_and_shown_and_server_errors_land_on_the_right_field()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        var viewModel = CreateViewModel();
        await viewModel.LoadAsync(Cancellation);

        LeaveFormViewModel? form = null;
        _dialogs.When<LeaveFormViewModel>(async opened =>
        {
            form = opened;
            opened.Employees.Count.ShouldBe(2);
            await opened.SubmitAsync(Cancellation);
            opened.GetError(nameof(opened.EditEmployee)).ShouldBe("Wybierz pracownika.");

            opened.EditEmployee = opened.Employees.Single(choice => choice.Id == employee.Id);
            opened.EditStartDate = new DateTime(2025, 6, 2);
            opened.EditEndDate.ShouldBe(new DateTime(2025, 6, 2));
            opened.EditEndDate = new DateTime(2025, 6, 6);
            await opened.SubmitAsync(Cancellation);

            opened.GetError(nameof(opened.EditKind)).ShouldBe("Najpierw ustaw wymiar urlopu pracownika na ten rok.");
            opened.Created.ShouldBeNull();

            opened.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.Unpaid);
            await opened.SubmitAsync(Cancellation);
        });
        await viewModel.NewAsync(Cancellation);

        form!.Title.ShouldBe("Nowy wniosek urlopowy");
        form.Created.ShouldNotBeNull();
        viewModel.Selected!.KindName.ShouldBe("Urlop bezpłatny");
        viewModel.Selected.DaysText.ShouldBe("5 dni");
        viewModel.Selected.Period.ShouldBe("2 cze – 6 cze 2025");
    }

    [Fact]
    public async Task Leave_in_hours_is_one_day_and_only_for_kinds_that_allow_it()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        var form = await CreateViewModel().CreateFormAsync(Cancellation);
        form.EditEmployee = form.Employees.Single(choice => choice.Id == employee.Id);

        form.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.OnDemand);
        form.CanUseHours.ShouldBeFalse();

        form.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.ChildCare);
        form.IsHourly = true;
        form.EditStartDate = new DateTime(2025, 6, 2);
        form.EditEndDate.ShouldBe(new DateTime(2025, 6, 2));

        form.EditHours = "dwie";
        await form.SubmitAsync(Cancellation);
        form.GetError(nameof(form.EditHours)).ShouldNotBeNull();

        form.EditHours = "9";
        await form.SubmitAsync(Cancellation);
        form.GetError(nameof(form.EditHours)).ShouldNotBeNull();
        form.Created.ShouldBeNull();

        form.EditHours = "2:30";
        await form.SubmitAsync(Cancellation);

        form.Created!.IsHourly.ShouldBeTrue();
        form.Created.Hours.ShouldBe(2.5m);
        LeaveTexts.ToRow(form.Created).DaysText.ShouldBe("2,5 godz.");

        var other = await CreateViewModel().CreateFormAsync(Cancellation);
        other.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.ChildCare);
        other.IsHourly = true;
        other.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.Unpaid);
        other.IsHourly.ShouldBeFalse();
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
