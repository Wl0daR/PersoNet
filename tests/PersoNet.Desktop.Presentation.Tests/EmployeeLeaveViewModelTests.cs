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
    private readonly FakeDialogService _dialogs = new();

    [Fact]
    public async Task Without_an_entitlement_says_so_and_saving_one_shows_the_balance()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        _leave.Add(employee.Id, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), LeaveState.Approved);
        var viewModel = new EmployeeLeaveViewModel(_leave, _time, _dialogs);

        await viewModel.ShowAsync(await _employees.GetAsync(employee.Id, Cancellation), Cancellation);

        viewModel.Year.ShouldBe(2025);
        viewModel.HasEntitlement.ShouldBeFalse();
        viewModel.Summary.ShouldContain("nie jest jeszcze ustawiony");
        viewModel.Items.ShouldHaveSingleItem();

        EntitlementDialogViewModel? dialog = null;
        _dialogs.When<EntitlementDialogViewModel>(opened =>
        {
            dialog = opened;
            opened.EditDays = "26";
            opened.EditCarriedOver = "4";
            return opened.SaveAsync(Cancellation);
        });
        await viewModel.EditEntitlementAsync(Cancellation);

        dialog!.Title.ShouldBe("Wymiar urlopu 2025 – Jan Nowak");

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
        var dialog = new EntitlementDialogViewModel(_leave, await _employees.GetAsync(employee.Id, Cancellation), 2025, balance: null);

        dialog.EditDays = days;
        dialog.EditCarriedOver = carriedOver;
        await dialog.SaveAsync(Cancellation);

        (dialog.GetError(nameof(dialog.EditDays)) ?? dialog.GetError(nameof(dialog.EditCarriedOver))).ShouldNotBeNull();
        dialog.HasChanges.ShouldBeFalse();
        _leave.Entitlements.ShouldBeEmpty();
    }

    [Fact]
    public async Task Calendar_shows_the_month_from_monday_with_leave_holidays_and_today()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        _leave.Add(employee.Id, new DateOnly(2025, 5, 12), new DateOnly(2025, 5, 14), LeaveState.Approved);
        _leave.Add(employee.Id, new DateOnly(2025, 5, 22), new DateOnly(2025, 5, 22));
        _leave.Add(employee.Id, new DateOnly(2025, 5, 26), new DateOnly(2025, 5, 26), LeaveState.Cancelled);
        var viewModel = new EmployeeLeaveViewModel(_leave, _time, _dialogs);

        await viewModel.ShowAsync(await _employees.GetAsync(employee.Id, Cancellation), Cancellation);

        viewModel.MonthTitle.ShouldBe("Maj 2025");
        viewModel.Days.Count.ShouldBe(42);
        viewModel.Days[0].Date.ShouldBe(new DateOnly(2025, 4, 28)); // 1 May 2025 is a Thursday
        viewModel.Days[0].IsInMonth.ShouldBeFalse();
        Day(viewModel, 1).HolidayName.ShouldBe("Święto Pracy");
        Day(viewModel, 1).IsDayOff.ShouldBeTrue();
        Day(viewModel, 13).IsApproved.ShouldBeTrue();
        Day(viewModel, 22).IsPending.ShouldBeTrue();
        Day(viewModel, 26).HasLeave.ShouldBeFalse(); // cancelled requests are not shown
        viewModel.SelectedDay!.Date.ShouldBe(new DateOnly(2025, 5, 20));
        viewModel.SelectedDay.IsToday.ShouldBeTrue();
        viewModel.CanAddOnSelectedDay.ShouldBeTrue();

        viewModel.NextMonth();
        viewModel.MonthTitle.ShouldBe("Czerwiec 2025");
        viewModel.Days[0].Date.ShouldBe(new DateOnly(2025, 5, 26));
    }

    [Fact]
    public async Task Moving_to_another_year_loads_that_years_leave_and_balance()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        _leave.Add(employee.Id, new DateOnly(2026, 1, 12), new DateOnly(2026, 1, 12), LeaveState.Approved);
        var viewModel = new EmployeeLeaveViewModel(_leave, _time, _dialogs);
        await viewModel.ShowAsync(await _employees.GetAsync(employee.Id, Cancellation), Cancellation);

        for (var i = 0; i < 8; i++)
        {
            viewModel.NextMonth();
        }

        viewModel.MonthTitle.ShouldBe("Styczeń 2026");
        viewModel.Balance!.Year.ShouldBe(2026);
        viewModel.Items.ShouldHaveSingleItem();
        viewModel.Days.Single(day => day.Date == new DateOnly(2026, 1, 12)).IsApproved.ShouldBeTrue();

        viewModel.ThisMonth();
        viewModel.MonthTitle.ShouldBe("Maj 2025");
    }

    [Fact]
    public async Task Leave_is_added_from_the_picked_day_in_a_window_and_then_shows_in_the_calendar()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        var viewModel = new EmployeeLeaveViewModel(_leave, _time, _dialogs);
        await viewModel.ShowAsync(await _employees.GetAsync(employee.Id, Cancellation), Cancellation);

        LeaveFormViewModel? form = null;
        _dialogs.When<LeaveFormViewModel>(opened =>
        {
            form = opened;
            opened.IsEmployeeFixed.ShouldBeTrue();
            opened.EditEmployee.Id.ShouldBe(employee.Id);
            opened.EditStartDate.ShouldBe(new DateTime(2025, 5, 26));
            opened.EditEndDate = new DateTime(2025, 5, 28);
            opened.EditKind = LeaveTexts.Kinds.Single(kind => kind.Kind == LeaveKind.Unpaid);
            return opened.SubmitAsync(Cancellation);
        });
        await viewModel.OpenDayAsync(Day(viewModel, 26), Cancellation);

        form!.Title.ShouldBe("Nowy wniosek urlopowy – Jan Nowak");
        Day(viewModel, 27).IsPending.ShouldBeTrue();
        viewModel.SelectedDay!.Date.ShouldBe(new DateOnly(2025, 5, 26));
        viewModel.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Double_click_on_a_day_with_leave_opens_its_request_and_on_a_holiday_does_nothing()
    {
        var employee = _employees.Add("P001", "Jan", "Nowak");
        _leave.Add(employee.Id, new DateOnly(2025, 5, 12), new DateOnly(2025, 5, 14));
        var viewModel = new EmployeeLeaveViewModel(_leave, _time, _dialogs);
        await viewModel.ShowAsync(await _employees.GetAsync(employee.Id, Cancellation), Cancellation);

        await viewModel.OpenDayAsync(Day(viewModel, 1), Cancellation);
        _dialogs.Shown.ShouldBeEmpty();

        _dialogs.When<LeaveDecisionViewModel>(opened => opened.ApproveAsync(Cancellation));
        await viewModel.OpenDayAsync(Day(viewModel, 13), Cancellation);

        _dialogs.Shown.ShouldHaveSingleItem().ShouldBeOfType<LeaveDecisionViewModel>();
        Day(viewModel, 13).IsApproved.ShouldBeTrue();
    }

    private static CalendarDay Day(EmployeeLeaveViewModel viewModel, int day) =>
        viewModel.Days.Single(cell => cell.IsInMonth && cell.Day == day);
}
