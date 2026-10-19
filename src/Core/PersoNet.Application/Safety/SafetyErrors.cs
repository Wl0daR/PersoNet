using PersoNet.Application.Common;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Safety;

namespace PersoNet.Application.Safety;

internal static class SafetyErrors
{
    public static readonly AppError NoTenant =
        AppError.Forbidden("tenant.missing", "Użytkownik nie jest przypisany do firmy.");

    public static readonly AppError CourseNotFound =
        AppError.NotFound("training_course.not_found", "Nie znaleziono szkolenia.");

    public static readonly AppError CourseNameRequired =
        AppError.Validation("training_course.name_required", "Podaj nazwę szkolenia.");

    public static readonly AppError CourseNameTaken =
        AppError.Conflict("training_course.name_taken", "Szkolenie o tej nazwie już jest na liście.");

    public static readonly AppError ValidityOutOfRange =
        AppError.Validation("training_course.validity_out_of_range", $"Ważność szkolenia musi wynosić od 1 do {TrainingCourse.MaxValidityMonths} miesięcy.");

    public static readonly AppError DepartmentNotFound =
        AppError.Validation("training_course.department_not_found", "Nie znaleziono jednego z wybranych działów.");

    public static readonly AppError EmployeeNotFound =
        AppError.NotFound("safety.employee_not_found", "Nie znaleziono pracownika.");

    public static readonly AppError ExamNotFound =
        AppError.NotFound("medical_exam.not_found", "Nie znaleziono badania.");

    public static readonly AppError ExamExpiresBeforeDate =
        AppError.Validation("medical_exam.expires_before_date", "Orzeczenie nie może tracić ważności przed dniem badania.");

    public static readonly AppError TrainingNotFound =
        AppError.NotFound("training.not_found", "Nie znaleziono szkolenia pracownika.");

    public static readonly AppError TrainingCourseRequired =
        AppError.Validation("training.course_not_found", "Wybierz szkolenie z listy.");

    public static readonly AppError TrainingExpiresBeforeDate =
        AppError.Validation("training.expires_before_date", "Szkolenie nie może tracić ważności przed dniem jego ukończenia.");

    public static readonly AppError NoteTooLong =
        AppError.Validation("safety.note_too_long", $"Uwagi mogą mieć najwyżej {MedicalExam.NoteMaxLength} znaków.");

    public static readonly AppError ImportEmpty =
        AppError.Validation("safety.import_empty", "Nie ma czego importować.");

    public static readonly AppError ImportTooLarge =
        AppError.Validation("safety.import_too_large", $"Jednorazowo można zaimportować najwyżej {SafetyImportService.MaxRows} wierszy każdego rodzaju.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("safety.invalid", exception.Message);
}
