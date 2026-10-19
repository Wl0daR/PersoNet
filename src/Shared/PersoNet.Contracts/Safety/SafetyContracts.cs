using System.Text.Json.Serialization;
using PersoNet.Contracts.Absences;

namespace PersoNet.Contracts.Safety;

/// <summary>Kind of occupational medical examination; sent as text, e.g. "Periodic".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<MedicalExamKind>))]
public enum MedicalExamKind
{
    /// <summary>Badanie wstępne.</summary>
    Initial = 1,

    /// <summary>Badanie okresowe.</summary>
    Periodic = 2,

    /// <summary>Badanie kontrolne.</summary>
    Control = 3,
}

/// <summary>
/// A training of the company's list. <paramref name="ValidityMonths"/> is null for one that does not expire; a mandatory
/// one with no <paramref name="DepartmentIds"/> concerns everyone.
/// </summary>
public sealed record TrainingCourseResponse(
    Guid Id,
    string Name,
    int? ValidityMonths,
    bool IsMandatory,
    IReadOnlyList<Guid> DepartmentIds,
    string? Description,
    bool IsActive);

/// <summary>A new training of the list or the changed one.</summary>
public sealed record SaveTrainingCourseRequest(
    string Name,
    int? ValidityMonths,
    bool IsMandatory,
    IReadOnlyList<Guid>? DepartmentIds,
    string? Description);

/// <summary>A medical examination; <paramref name="IsCurrent"/> marks the latest one, which decides whether the employee may work.</summary>
public sealed record MedicalExamResponse(
    Guid Id,
    Guid EmployeeId,
    MedicalExamKind Kind,
    DateOnly ExamDate,
    DateOnly? ValidUntil,
    string? Note,
    bool IsCurrent);

public sealed record SaveMedicalExamRequest(MedicalExamKind Kind, DateOnly ExamDate, DateOnly? ValidUntil, string? Note);

/// <summary>A training the employee completed; <paramref name="IsCurrent"/> marks the latest one of that course.</summary>
public sealed record EmployeeTrainingResponse(
    Guid Id,
    Guid EmployeeId,
    Guid CourseId,
    string CourseName,
    DateOnly CompletedOn,
    DateOnly? ValidUntil,
    string? Note,
    bool IsCurrent);

/// <summary>
/// A completed training. Without <paramref name="ValidUntil"/> the training is valid as long as the course says
/// (for ever when the course does not expire).
/// </summary>
public sealed record SaveEmployeeTrainingRequest(Guid CourseId, DateOnly CompletedOn, DateOnly? ValidUntil, string? Note);

/// <summary>
/// Medical examinations and trainings of an employee, the latest first, and the mandatory trainings the employee has
/// never had (<paramref name="MissingCourses"/>).
/// </summary>
public sealed record EmployeeSafetyResponse(
    IReadOnlyList<MedicalExamResponse> Exams,
    IReadOnlyList<EmployeeTrainingResponse> Trainings,
    IReadOnlyList<TrainingCourseResponse> MissingCourses);

/// <summary>
/// A training of the earlier system's list. <paramref name="DepartmentCodes"/> are codes of departments already imported
/// (e.g. "D12"); a training of the same name already in PersoNet is left as it is.
/// </summary>
public sealed record ImportTrainingCourseRow(
    int Line,
    string Name,
    int? ValidityMonths,
    bool IsMandatory,
    IReadOnlyList<string>? DepartmentCodes,
    string? Description);

/// <summary>A training completed by the employee with this <paramref name="Number"/>, of the course with this name.</summary>
public sealed record ImportTrainingRow(int Line, string Number, string CourseName, DateOnly CompletedOn, DateOnly? ValidUntil, string? Note);

/// <summary>A medical examination of the employee with this <paramref name="Number"/>.</summary>
public sealed record ImportMedicalExamRow(int Line, string Number, MedicalExamKind Kind, DateOnly ExamDate, DateOnly? ValidUntil, string? Note);

/// <summary>Trainings of the list, completed trainings and medical examinations, at most 5000 rows of each in one request.</summary>
public sealed record ImportSafetyRequest(
    IReadOnlyList<ImportTrainingCourseRow> Courses,
    IReadOnlyList<ImportTrainingRow> Trainings,
    IReadOnlyList<ImportMedicalExamRow> Exams);

/// <summary>
/// What an import did. A row already in PersoNet (the same course name; the same employee, course or examination and
/// date) counts as existing, so the import can run again.
/// </summary>
public sealed record ImportSafetyResponse(
    int CoursesAdded,
    int TrainingsAdded,
    int ExamsAdded,
    int Existing,
    IReadOnlyList<ImportLeaveProblem> Problems);
