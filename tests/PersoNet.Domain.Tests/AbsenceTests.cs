using PersoNet.Domain.Absences;
using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Tests;

public sealed class AbsenceTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid EmployeeId = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2025, 6, 1, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_sick_note_counts_every_calendar_day_and_is_sickness()
    {
        var absence = Absence.Create(TenantId, EmployeeId, AbsenceType.Sickness, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 9), "  e-ZLA  ", Now);

        absence.CalendarDays.ShouldBe(7);
        absence.IsSickness.ShouldBeTrue();
        absence.IsWorkElsewhere.ShouldBeFalse();
        absence.Note.ShouldBe("e-ZLA");
    }

    [Theory]
    [InlineData(AbsenceType.BusinessTrip, true)]
    [InlineData(AbsenceType.RemoteWork, true)]
    [InlineData(AbsenceType.Unexcused, false)]
    [InlineData(AbsenceType.MedicalExam, false)]
    public void Business_trips_training_fairs_and_remote_work_are_work_done_elsewhere(AbsenceType type, bool elsewhere) =>
        Absence.IsWorkElsewhereType(type).ShouldBe(elsewhere);

    [Fact]
    public void An_absence_cannot_end_before_it_starts_nor_have_an_unknown_kind_or_a_long_note()
    {
        Should.Throw<DomainException>(() => Absence.Create(TenantId, EmployeeId, AbsenceType.Training, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 2), null, Now));
        Should.Throw<DomainException>(() => Absence.Create(TenantId, EmployeeId, (AbsenceType)99, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), null, Now));
        Should.Throw<DomainException>(() => Absence.Create(TenantId, EmployeeId, AbsenceType.Training, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), new string('x', 501), Now));
        Should.Throw<DomainException>(() => Absence.Create(TenantId, Guid.Empty, AbsenceType.Training, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3), null, Now));
    }

    [Fact]
    public void Periods_sharing_a_day_overlap()
    {
        var absence = Absence.Create(TenantId, EmployeeId, AbsenceType.RemoteWork, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 5), null, Now);

        absence.Overlaps(new DateOnly(2025, 3, 5), new DateOnly(2025, 3, 6)).ShouldBeTrue();
        absence.Overlaps(new DateOnly(2025, 3, 6), new DateOnly(2025, 3, 7)).ShouldBeFalse();
    }
}
