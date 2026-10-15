using System.Text.Json.Serialization;
using PersoNet.Contracts.Leave;

namespace PersoNet.Contracts.Absences;

/// <summary>Kind of absence that is not leave; sent as text, e.g. "Sickness".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AbsenceKind>))]
public enum AbsenceKind
{
    /// <summary>Choroba (L4).</summary>
    Sickness = 1,

    /// <summary>Opieka nad chorym dzieckiem (zasiłek opiekuńczy).</summary>
    ChildCareAllowance = 2,

    /// <summary>Opieka nad innym członkiem rodziny (zasiłek opiekuńczy).</summary>
    FamilyCareAllowance = 3,

    /// <summary>Świadczenie rehabilitacyjne.</summary>
    Rehabilitation = 4,

    /// <summary>Choroba po wypadku przy pracy.</summary>
    WorkAccident = 5,

    /// <summary>Nieobecność nieusprawiedliwiona.</summary>
    Unexcused = 6,

    /// <summary>Inna nieobecność usprawiedliwiona.</summary>
    OtherExcused = 7,

    /// <summary>Delegacja.</summary>
    BusinessTrip = 8,

    /// <summary>Szkolenie.</summary>
    Training = 9,

    /// <summary>Badania lekarskie.</summary>
    MedicalExam = 10,

    /// <summary>Targi.</summary>
    TradeFair = 11,

    /// <summary>Praca zdalna (home office).</summary>
    RemoteWork = 12,
}

/// <summary>
/// An absence of an employee. <see cref="CalendarDays"/> count every day of the period, as sick notes do;
/// <see cref="WorkingDays"/> leave out weekends and public holidays.
/// </summary>
public sealed record AbsenceResponse(
    Guid Id,
    Guid EmployeeId,
    AbsenceKind Kind,
    DateOnly StartDate,
    DateOnly EndDate,
    int CalendarDays,
    int WorkingDays,
    string? Note);

/// <summary>A new absence or the changed one.</summary>
public sealed record SaveAbsenceRequest(AbsenceKind Kind, DateOnly StartDate, DateOnly EndDate, string? Note);

/// <summary>
/// Leave taken in the earlier system, for the employee with this <paramref name="Number"/>. Recorded as approved, without
/// the balance and limit checks of new requests; a period over the turn of a year is split.
/// </summary>
public sealed record ImportLeaveRow(int Line, string Number, LeaveKind Kind, DateOnly StartDate, DateOnly EndDate);

/// <summary>An absence from the earlier system, for the employee with this <paramref name="Number"/>, with an optional note.</summary>
public sealed record ImportAbsenceRow(int Line, string Number, AbsenceKind Kind, DateOnly StartDate, DateOnly EndDate, string? Note = null);

/// <summary>
/// The annual leave days of an employee in a year and the unused days carried over from the year before, from the earlier
/// system. They replace what PersoNet holds for that year.
/// </summary>
public sealed record ImportEntitlementRow(int Line, string Number, int Year, int Days, int CarriedOverDays);

/// <summary>Leave, absences and entitlements of already imported employees, at most 5000 rows of each in one request.</summary>
public sealed record ImportLeaveHistoryRequest(
    IReadOnlyList<ImportLeaveRow> Leave,
    IReadOnlyList<ImportAbsenceRow> Absences,
    IReadOnlyList<ImportEntitlementRow> Entitlements);

/// <summary>
/// What an import of leave history did. Rows already present (same employee, kind and days, or the same entitlement)
/// count as existing, so the import can run again; <paramref name="EntitlementsSaved"/> are the years set or changed.
/// </summary>
public sealed record ImportLeaveHistoryResponse(
    int LeaveAdded,
    int AbsencesAdded,
    int EntitlementsSaved,
    int Existing,
    IReadOnlyList<ImportLeaveProblem> Problems);

/// <summary>A row that was not imported, with the reason.</summary>
public sealed record ImportLeaveProblem(int Line, string Number, string Message);
