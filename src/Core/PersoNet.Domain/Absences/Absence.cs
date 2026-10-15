using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Absences;

/// <summary>Kinds of time away from the usual workplace that are not leave: sickness, unexcused absence, work elsewhere.</summary>
public enum AbsenceType
{
    /// <summary>Choroba pracownika (zwolnienie lekarskie, L4).</summary>
    Sickness = 1,

    /// <summary>Opieka nad chorym dzieckiem (zasiłek opiekuńczy).</summary>
    ChildCareAllowance = 2,

    /// <summary>Opieka nad innym chorym członkiem rodziny (zasiłek opiekuńczy).</summary>
    FamilyCareAllowance = 3,

    /// <summary>Świadczenie rehabilitacyjne po wyczerpaniu zasiłku chorobowego.</summary>
    Rehabilitation = 4,

    /// <summary>Choroba po wypadku przy pracy.</summary>
    WorkAccident = 5,

    /// <summary>Nieobecność nieusprawiedliwiona.</summary>
    Unexcused = 6,

    /// <summary>Inna nieobecność usprawiedliwiona.</summary>
    OtherExcused = 7,

    /// <summary>Podróż służbowa (delegacja): work, away from the workplace.</summary>
    BusinessTrip = 8,

    /// <summary>Szkolenie: work, away from the workplace.</summary>
    Training = 9,

    /// <summary>Badania lekarskie (wstępne, okresowe, kontrolne).</summary>
    MedicalExam = 10,

    /// <summary>Targi: work, away from the workplace.</summary>
    TradeFair = 11,

    /// <summary>Praca zdalna (home office).</summary>
    RemoteWork = 12,
}

/// <summary>
/// A period an employee is away from the usual workplace for a reason other than leave: sickness (L4), an unexcused or
/// other absence, or work done elsewhere (a business trip, training, remote work). HR records it from a document or a
/// decision; it needs no approval and does not use the leave entitlement.
/// </summary>
public sealed class Absence : Entity, ITenantOwned
{
    public const int NoteMaxLength = 500;

    // Used by EF Core.
    private Absence()
    {
    }

    private Absence(Guid tenantId, Guid employeeId, DateTimeOffset createdAt)
    {
        TenantId = tenantId;
        EmployeeId = employeeId;
        CreatedAt = createdAt;
    }

    public Guid TenantId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public AbsenceType Type { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>Last day of the absence (inclusive).</summary>
    public DateOnly EndDate { get; private set; }

    public string? Note { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>Calendar days the absence spans, as sick notes count them.</summary>
    public int CalendarDays => EndDate.DayNumber - StartDate.DayNumber + 1;

    /// <summary>True for sickness and the allowances paid instead of wages (ZUS), not for work elsewhere.</summary>
    public bool IsSickness => IsSicknessType(Type);

    /// <summary>True when the employee works, only not at the usual workplace.</summary>
    public bool IsWorkElsewhere => IsWorkElsewhereType(Type);

    public static bool IsSicknessType(AbsenceType type) => type is >= AbsenceType.Sickness and <= AbsenceType.WorkAccident;

    public static bool IsWorkElsewhereType(AbsenceType type) => type is AbsenceType.BusinessTrip or AbsenceType.Training or AbsenceType.TradeFair or AbsenceType.RemoteWork;

    public static Absence Create(Guid tenantId, Guid employeeId, AbsenceType type, DateOnly startDate, DateOnly endDate, string? note, DateTimeOffset createdAt)
    {
        if (tenantId == Guid.Empty || employeeId == Guid.Empty)
        {
            throw new DomainException("Absence must belong to a tenant and an employee.");
        }

        var absence = new Absence(tenantId, employeeId, createdAt);
        absence.Change(type, startDate, endDate, note);
        return absence;
    }

    public void Change(AbsenceType type, DateOnly startDate, DateOnly endDate, string? note)
    {
        if (!Enum.IsDefined(type))
        {
            throw new DomainException("Unknown absence type.");
        }

        if (endDate < startDate)
        {
            throw new DomainException("Absence cannot end before it starts.");
        }

        var cleaned = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (cleaned is { Length: > NoteMaxLength })
        {
            throw new DomainException($"A note can have at most {NoteMaxLength} characters.");
        }

        Type = type;
        StartDate = startDate;
        EndDate = endDate;
        Note = cleaned;
    }

    /// <summary>True when both periods share at least one day.</summary>
    public bool Overlaps(DateOnly start, DateOnly end) => StartDate <= end && start <= EndDate;
}
