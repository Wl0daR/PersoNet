using PersoNet.Contracts.Leave;
using PersoNet.Web.Portal;

namespace PersoNet.Web.Tests;

public sealed class LeaveCalendarMonthTests
{
    private static readonly LeaveCalendarPerson Ala = new(
        Guid.NewGuid(), "Ala", "Nowak", null, null,
        [
            new LeaveCalendarEntry(Guid.NewGuid(), LeaveKind.Annual, new DateOnly(2025, 5, 28), new DateOnly(2025, 6, 10), LeaveState.Approved, false, 64),
            new LeaveCalendarEntry(Guid.NewGuid(), LeaveKind.Unpaid, new DateOnly(2025, 6, 10), new DateOnly(2025, 6, 11), LeaveState.Pending, false, 16),
        ]);

    private static LeaveCalendarMonth June() => new(
        new LeaveCalendarResponse(2025, 6, [new HolidayResponse(new DateOnly(2025, 6, 19), "Boże Ciało")], [Ala]),
        new DateOnly(2025, 6, 2));

    [Fact]
    public void Lays_out_every_day_with_weekends_holidays_and_today()
    {
        var june = June();

        june.Title.ShouldBe("czerwiec 2025");
        june.Days.Count.ShouldBe(30);
        june.Days[0].DayName.ShouldBe("nd");
        june.Days[0].IsDayOff.ShouldBeTrue();
        june.Days[1].IsToday.ShouldBeTrue();
        june.Days[18].Holiday.ShouldBe("Boże Ciało");
    }

    [Fact]
    public void Marks_working_days_of_leave_and_prefers_approved_leave_on_a_shared_day()
    {
        var cells = June().Rows.Single().Cells;

        cells[0].Entry.ShouldBeNull();
        cells[1].Entry!.Kind.ShouldBe(LeaveKind.Annual);
        cells[9].Entry!.State.ShouldBe(LeaveState.Approved);
        cells[10].Entry!.State.ShouldBe(LeaveState.Pending);
        cells[11].Entry.ShouldBeNull();
        cells[10].Description.ShouldBe("11 czerwca: urlop bezpłatny, czeka na decyzję");
        cells[18].Description.ShouldBe("19 czerwca: Boże Ciało");
    }

    [Fact]
    public void Counts_who_is_away_and_lists_absences_by_start()
    {
        var june = June();

        june.AwayOn(new DateOnly(2025, 6, 2)).ShouldBe(1);
        june.AwayOn(new DateOnly(2025, 6, 11)).ShouldBe(0);
        june.Absences.Select(item => item.Entry.Kind).ShouldBe([LeaveKind.Annual, LeaveKind.Unpaid]);
        LeaveCalendarMonth.Mark(LeaveKind.OnDemand).ShouldBe("Ż");
    }
}
