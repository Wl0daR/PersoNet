using PersoNet.Application.Common;
using PersoNet.Domain.Absences;
using PersoNet.Domain.Abstractions;

namespace PersoNet.Application.Absences;

internal static class AbsenceErrors
{
    public static readonly AppError NotFound =
        AppError.NotFound("absence.not_found", "Nie znaleziono nieobecności.");

    public static readonly AppError EmployeeNotFound =
        AppError.Validation("absence.employee_not_found", "Wybierz pracownika z listy.");

    public static readonly AppError EndBeforeStart =
        AppError.Validation("absence.end_before_start", "Nieobecność nie może kończyć się przed pierwszym dniem.");

    public static readonly AppError Overlap =
        AppError.Conflict("absence.overlap", "W tym okresie pracownik ma już wpisaną inną nieobecność.");

    public static readonly AppError NoteTooLong =
        AppError.Validation("absence.note_too_long", $"Uwagi mogą mieć najwyżej {Absence.NoteMaxLength} znaków.");

    public static readonly AppError YearOutOfRange =
        AppError.Validation("absence.year_out_of_range", "Rok musi być z zakresu 2000–2100.");

    public static readonly AppError NoTenant =
        AppError.Forbidden("tenant.missing", "Użytkownik nie jest przypisany do firmy.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("absence.invalid", exception.Message);
}
