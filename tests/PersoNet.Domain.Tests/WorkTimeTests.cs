using System.Globalization;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.WorkTime;

namespace PersoNet.Domain.Tests;

public sealed class WorkTimeTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid Employee = Guid.CreateVersion7();

    [Theory]
    [InlineData("2025-11-03", DayKind.Working)]
    [InlineData("2025-11-08", DayKind.DayOff)]
    [InlineData("2025-11-09", DayKind.Sunday)]
    [InlineData("2025-11-11", DayKind.Holiday)]
    [InlineData("2025-11-01", DayKind.Holiday)] // All Saints' Day on a Saturday
    public void Days_are_classified_for_the_basic_working_time_system(string date, DayKind kind) =>
        WorkTimeRules.Classify(Day(date)).ShouldBe(kind);

    [Fact]
    public void A_holiday_on_saturday_lowers_the_norm_of_the_month_but_not_the_planned_days()
    {
        // November 2025: 20 days Monday to Friday, holidays on Saturday the 1st and Tuesday the 11th.
        var november = Enumerable.Range(1, 30).Select(day => new DateOnly(2025, 11, day)).ToList();

        november.Sum(day => WorkTimeRules.NormMinutes(day, 1m)).ShouldBe(144 * 60);
        november.Sum(day => WorkTimeRules.PlannedMinutes(day, 1m)).ShouldBe(152 * 60);
        november.Sum(day => WorkTimeRules.NormMinutes(day, 0.5m)).ShouldBe(72 * 60);
        november.Sum(day => WorkTimeRules.NormMinutes(day, null)).ShouldBe(0);
    }

    [Theory]
    [InlineData("22:00", "06:00", 480, 480)]
    [InlineData("20:00", "23:00", 180, 60)]
    [InlineData("05:00", "13:00", 480, 60)]
    [InlineData("08:00", "16:00", 480, 0)]
    [InlineData("21:00", "23:00", 60, 60)] // An hour of break: never more night than work
    public void Night_work_is_the_part_of_the_shift_between_22_and_6(string start, string end, int worked, int night) =>
        WorkTimeRules.NightMinutes(Time(start), Time(end), worked).ShouldBe(night);

    [Fact]
    public void Overtime_is_counted_over_8_hours_and_part_time_hours_over_the_plan_separately()
    {
        var monday = Day("2025-11-03");

        WorkTimeRules.Figures(monday, 1m, 600, 0).ShouldBe(new DayFigures(DayKind.Working, 480, 600, 120, 0, 0, 0, 0));
        WorkTimeRules.Figures(monday, 0.5m, 360, 0).ShouldBe(new DayFigures(DayKind.Working, 240, 360, 0, 120, 0, 0, 0));
        WorkTimeRules.Figures(Day("2025-11-08"), 1m, 300, 0).OnDayOff.ShouldBe(300);
        WorkTimeRules.Figures(Day("2025-11-11"), 1m, 300, 0).ShouldBe(new DayFigures(DayKind.Holiday, 0, 300, 0, 0, 0, 300, 0));
    }

    [Fact]
    public void Clock_events_of_a_day_give_the_first_in_the_last_out_and_the_time_out_between()
    {
        var days = ClockPairing.Pair(
        [
            (At("2025-11-03 07:58:40"), ClockDirection.In),
            (At("2025-11-03 07:59:30"), ClockDirection.In), // the card tapped twice
            (At("2025-11-03 12:00:10"), ClockDirection.Out),
            (At("2025-11-03 12:30:00"), ClockDirection.In),
            (At("2025-11-03 16:30:00"), ClockDirection.Out),
        ]);

        days.ShouldBe([new ClockDay(Day("2025-11-03"), Time("07:58"), Time("16:30"), 30, ClockProblem.None)]);
    }

    [Fact]
    public void A_night_shift_belongs_to_the_day_it_started()
    {
        var days = ClockPairing.Pair([(At("2025-11-03 22:00:00"), ClockDirection.In), (At("2025-11-04 06:00:00"), ClockDirection.Out)]);

        days.ShouldBe([new ClockDay(Day("2025-11-03"), Time("22:00"), Time("06:00"), 0, ClockProblem.None)]);
    }

    [Fact]
    public void Missing_clock_events_mark_the_day()
    {
        var days = ClockPairing.Pair(
        [
            (At("2025-11-03 08:00:00"), ClockDirection.In),
            (At("2025-11-04 08:00:00"), ClockDirection.In),
            (At("2025-11-04 16:00:00"), ClockDirection.Out),
            (At("2025-11-05 16:00:00"), ClockDirection.Out),
        ]);

        days.Select(day => (day.Date, day.Problem)).ShouldBe(
        [
            (Day("2025-11-03"), ClockProblem.MissingOut),
            (Day("2025-11-04"), ClockProblem.None),
            (Day("2025-11-05"), ClockProblem.MissingIn),
        ]);
    }

    [Fact]
    public void A_day_typed_by_hand_counts_its_hours_and_is_not_changed_by_the_clock()
    {
        var day = WorkDay.Worked(Tenant, Employee, Day("2025-11-03"), Time("22:00"), Time("06:30"), 30, "  zmiana nocna ");

        day.WorkedMinutes.ShouldBe(480);
        day.Note.ShouldBe("zmiana nocna");
        day.Origin.ShouldBe(WorkDayOrigin.Manual);
        Should.Throw<DomainException>(() => day.ApplyClock(new ClockDay(day.Date, Time("08:00"), Time("16:00"), 0, ClockProblem.None)));
    }

    [Fact]
    public void A_day_from_the_clock_with_a_missing_event_counts_no_hours_until_corrected()
    {
        var day = WorkDay.FromClock(Tenant, Employee, new ClockDay(Day("2025-11-03"), Time("08:00"), null, 0, ClockProblem.MissingOut));

        day.WorkedMinutes.ShouldBe(0);

        day.ChangeToWork(Time("08:00"), Time("16:00"), 0, null);

        day.WorkedMinutes.ShouldBe(480);
        day.Problem.ShouldBe(ClockProblem.None);
        day.Origin.ShouldBe(WorkDayOrigin.Manual);
    }

    [Theory]
    [InlineData("08:00", "08:00", 0)]
    [InlineData("08:00", "09:00", 60)]
    [InlineData("08:00", "16:00", -1)]
    public void Work_needs_an_end_and_a_break_shorter_than_the_shift(string start, string end, int breakMinutes) =>
        Should.Throw<DomainException>(() => WorkDay.Worked(Tenant, Employee, Day("2025-11-03"), Time(start), Time(end), breakMinutes, null));

    private static DateOnly Day(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

    private static TimeOnly Time(string value) => TimeOnly.Parse(value, CultureInfo.InvariantCulture);

    private static DateTime At(string value) => DateTime.Parse(value, CultureInfo.InvariantCulture);
}
