using Microsoft.Extensions.Time.Testing;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Leave;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class TeamCalendarViewModelTests
{
    private readonly FakeLeaveApi _leave = new();
    private readonly FakeOrganizationApi _organization = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 6, 2, 9, 0, 0, TimeSpan.Zero));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private TeamCalendarViewModel Screen() => new(_leave, _organization, _time);

    [Fact]
    public async Task Shows_the_current_month_with_a_mark_on_each_working_day_of_leave()
    {
        var ala = Person("Ala", "Nowak", null);
        Leave(ala, LeaveKind.Annual, new DateOnly(2025, 5, 30), new DateOnly(2025, 6, 9), LeaveState.Approved);
        Leave(ala, LeaveKind.Unpaid, new DateOnly(2025, 6, 20), new DateOnly(2025, 6, 20), LeaveState.Pending);
        var screen = Screen();

        await screen.LoadAsync(Cancellation);

        screen.MonthTitle.ShouldBe("Czerwiec 2025");
        screen.Days.Count.ShouldBe(30);
        screen.Days[1].IsToday.ShouldBeTrue();
        var cells = screen.Rows.Single().Cells;
        cells[1].Mark.ShouldBe("W");
        cells[1].IsApproved.ShouldBeTrue();
        cells[6].Mark.ShouldBeEmpty();
        cells[19].IsPending.ShouldBeTrue();
        cells[19].Description.ShouldBe("20 czerwca: urlop bezpłatny, czeka na decyzję");
        screen.Summary.ShouldBe("1 osoba · dziś na urlopie: 1");
    }

    [Fact]
    public async Task Lists_departments_under_their_parents_and_reloads_for_the_picked_one()
    {
        var sales = _organization.Add("Sprzedaż", "SPR");
        _organization.Add("Północ", "SPN", sales.Id);
        Person("Ala", "Nowak", sales.Id);
        Person("Bartek", "Adamski", null);
        var screen = Screen();
        await screen.LoadAsync(Cancellation);

        screen.Department = screen.Departments.Single(choice => choice.Id == sales.Id);
        await screen.LoadAsync(Cancellation);

        screen.Departments.Select(choice => choice.Name).ShouldBe(["Cała firma", "Sprzedaż", "    Północ"]);
        _leave.CalendarDepartment.ShouldBe(sales.Id);
        screen.Rows.Select(row => row.Name).ShouldBe(["Nowak Ala"]);
    }

    [Fact]
    public async Task Moves_between_months_and_back_to_the_current_one()
    {
        var screen = Screen();
        await screen.LoadAsync(Cancellation);

        await screen.NextMonthAsync(Cancellation);
        screen.MonthTitle.ShouldBe("Lipiec 2025");
        screen.IsCurrentMonth.ShouldBeFalse();
        screen.Days.Count.ShouldBe(31);
        screen.Summary.ShouldBe("0 osób");
        screen.IsEmpty.ShouldBeTrue();

        await screen.CurrentMonthAsync(Cancellation);
        screen.IsCurrentMonth.ShouldBeTrue();
    }

    private Guid Person(string firstName, string lastName, Guid? departmentId)
    {
        var id = Guid.NewGuid();
        _leave.People.Add((id, firstName, lastName, departmentId));
        return id;
    }

    private void Leave(Guid employeeId, LeaveKind kind, DateOnly start, DateOnly end, LeaveState state) =>
        _leave.Requests.Add(new LeaveRequestResponse(
            Guid.NewGuid(), employeeId, "P1", "Nowak Ala", kind, start, end, 1, 8, false, null, state, DateTimeOffset.UtcNow, null, null));
}
