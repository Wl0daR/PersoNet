using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Leave;

namespace PersoNet.Domain.Tests;

public sealed class LeaveTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid Employee = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2025, 4, 1, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(2024, "2024-03-31")]
    [InlineData(2025, "2025-04-20")]
    [InlineData(2026, "2026-04-05")]
    [InlineData(2027, "2027-03-28")]
    public void Easter_is_computed_for_the_gregorian_calendar(int year, string expected) =>
        PolishCalendar.EasterSunday(year).ShouldBe(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));

    [Theory]
    [InlineData("2025-01-06", true)] // Trzech Króli
    [InlineData("2025-04-21", true)] // Easter Monday
    [InlineData("2025-06-19", true)] // Corpus Christi
    [InlineData("2025-11-11", true)]
    [InlineData("2024-12-24", false)] // Christmas Eve only from 2025
    [InlineData("2025-12-24", true)]
    [InlineData("2025-04-22", false)]
    public void Public_holidays_are_known(string date, bool holiday) =>
        PolishCalendar.IsPublicHoliday(DateOnly.Parse(date, System.Globalization.CultureInfo.InvariantCulture)).ShouldBe(holiday);

    [Fact]
    public void Working_days_skip_weekends_and_holidays()
    {
        // Mon 28 April – Tue 6 May 2025: 1 May is a holiday, 3–4 May a weekend.
        PolishCalendar.WorkingDays(new DateOnly(2025, 4, 28), new DateOnly(2025, 5, 6)).ShouldBe(6);
    }

    [Fact]
    public void Request_counts_working_days_and_starts_pending()
    {
        var request = LeaveRequest.Create(Tenant, Employee, LeaveType.Annual, new DateOnly(2025, 4, 28), new DateOnly(2025, 5, 6), "  Majówka ", Now);

        request.Days.ShouldBe(6);
        request.Status.ShouldBe(LeaveStatus.Pending);
        request.Note.ShouldBe("Majówka");
        request.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("2025-05-10", "2025-05-11")] // weekend only
    [InlineData("2025-05-06", "2025-05-05")] // ends before start
    [InlineData("2025-12-29", "2026-01-02")] // crosses the year
    public void Invalid_periods_are_refused(string start, string end) =>
        Should.Throw<DomainException>(() => LeaveRequest.Create(
            Tenant, Employee, LeaveType.Annual, DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), null, Now));

    [Fact]
    public void Decision_is_made_once_and_rejection_needs_a_reason()
    {
        var request = LeaveRequest.Create(Tenant, Employee, LeaveType.Annual, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6), null, Now);
        var hr = Guid.CreateVersion7();

        Should.Throw<DomainException>(() => request.Reject(hr, Now, " "));
        request.Approve(hr, Now, null);

        request.Status.ShouldBe(LeaveStatus.Approved);
        request.DecidedBy.ShouldBe(hr);
        Should.Throw<DomainException>(() => request.Approve(hr, Now, null));

        request.Cancel(hr, Now, "Zmiana planów");
        request.IsActive.ShouldBeFalse();
        Should.Throw<DomainException>(() => request.Cancel(hr, Now, null));
    }

    [Fact]
    public void Entitlement_adds_carried_over_days_and_stays_in_range()
    {
        var entitlement = LeaveEntitlement.Create(Tenant, Employee, 2025, 26, 4);

        entitlement.Total.ShouldBe(30);
        Should.Throw<DomainException>(() => entitlement.Change(-1, 0));
        Should.Throw<DomainException>(() => entitlement.Change(26, 61));
    }
}
