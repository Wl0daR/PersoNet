using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Safety;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Safety;

namespace PersoNet.Application.Safety;

/// <summary>
/// An employee's occupational medical examinations and completed trainings, as HR records them from the doctor's
/// certificate and the training certificate, and the mandatory trainings the employee has not had yet.
/// </summary>
public sealed class EmployeeSafetyService(IAppDbContext db, ITenantContext tenant, TimeProvider time)
{
    public async Task<Result<EmployeeSafetyResponse>> GetAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var employee = await db.Employees.AsNoTracking()
            .Where(employee => employee.Id == employeeId)
            .Select(employee => new { employee.Id, employee.DepartmentId })
            .FirstOrDefaultAsync(cancellationToken);
        if (employee is null)
        {
            return SafetyErrors.EmployeeNotFound;
        }

        var exams = await db.MedicalExams.AsNoTracking()
            .Where(exam => exam.EmployeeId == employeeId)
            .OrderByDescending(exam => exam.ExamDate).ThenByDescending(exam => exam.CreatedAt)
            .ToListAsync(cancellationToken);
        var trainings = await db.EmployeeTrainings.AsNoTracking()
            .Where(training => training.EmployeeId == employeeId)
            .OrderByDescending(training => training.CompletedOn).ThenByDescending(training => training.CreatedAt)
            .ToListAsync(cancellationToken);
        var courses = await db.TrainingCourses.AsNoTracking().ToDictionaryAsync(course => course.Id, cancellationToken);
        var departments = await DepartmentTree.LoadAsync(db, cancellationToken);

        var latestOfCourse = trainings.GroupBy(training => training.CourseId).ToDictionary(group => group.Key, group => group.First().Id);
        var chain = departments.WithAncestors(employee.DepartmentId);
        var missing = courses.Values
            .Where(course => course.IsMandatoryFor(chain) && !latestOfCourse.ContainsKey(course.Id))
            .OrderBy(course => course.Name, StringComparer.CurrentCulture)
            .Select(TrainingCourseService.ToResponse)
            .ToList();

        return new EmployeeSafetyResponse(
            exams.Select((exam, index) => ToResponse(exam, isCurrent: index == 0)).ToList(),
            trainings.Select(training => ToResponse(training, courses.GetValueOrDefault(training.CourseId)?.Name, latestOfCourse[training.CourseId] == training.Id)).ToList(),
            missing);
    }

    public async Task<Result<MedicalExamResponse>> CreateExamAsync(Guid employeeId, SaveMedicalExamRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return SafetyErrors.NoTenant;
        }

        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return SafetyErrors.EmployeeNotFound;
        }

        if (Check(request) is { } error)
        {
            return error;
        }

        MedicalExam exam;
        try
        {
            exam = MedicalExam.Create(tenantId, employeeId, (MedicalExamType)request.Kind, request.ExamDate, request.ValidUntil, request.Note, time.GetUtcNow());
        }
        catch (DomainException exception)
        {
            return SafetyErrors.Invalid(exception);
        }

        db.MedicalExams.Add(exam);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(exam, await IsLatestExamAsync(exam, cancellationToken));
    }

    public async Task<Result<MedicalExamResponse>> UpdateExamAsync(Guid id, SaveMedicalExamRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var exam = await db.MedicalExams.FirstOrDefaultAsync(exam => exam.Id == id, cancellationToken);
        if (exam is null)
        {
            return SafetyErrors.ExamNotFound;
        }

        if (Check(request) is { } error)
        {
            return error;
        }

        try
        {
            exam.Change((MedicalExamType)request.Kind, request.ExamDate, request.ValidUntil, request.Note);
        }
        catch (DomainException exception)
        {
            return SafetyErrors.Invalid(exception);
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(exam, await IsLatestExamAsync(exam, cancellationToken));
    }

    public async Task<Result> DeleteExamAsync(Guid id, CancellationToken cancellationToken)
    {
        var exam = await db.MedicalExams.FirstOrDefaultAsync(exam => exam.Id == id, cancellationToken);
        if (exam is null)
        {
            return SafetyErrors.ExamNotFound;
        }

        db.MedicalExams.Remove(exam);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    public async Task<Result<EmployeeTrainingResponse>> CreateTrainingAsync(Guid employeeId, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return SafetyErrors.NoTenant;
        }

        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return SafetyErrors.EmployeeNotFound;
        }

        var course = await db.TrainingCourses.AsNoTracking().FirstOrDefaultAsync(course => course.Id == request.CourseId, cancellationToken);
        if (course is null)
        {
            return SafetyErrors.TrainingCourseRequired;
        }

        var validUntil = request.ValidUntil ?? course.ValidUntil(request.CompletedOn);
        if (Check(request.CompletedOn, validUntil, request.Note) is { } error)
        {
            return error;
        }

        var training = EmployeeTraining.Create(tenantId, employeeId, course.Id, request.CompletedOn, validUntil, request.Note, time.GetUtcNow());
        db.EmployeeTrainings.Add(training);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(training, course.Name, await IsLatestTrainingAsync(training, cancellationToken));
    }

    public async Task<Result<EmployeeTrainingResponse>> UpdateTrainingAsync(Guid id, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var training = await db.EmployeeTrainings.FirstOrDefaultAsync(training => training.Id == id, cancellationToken);
        if (training is null)
        {
            return SafetyErrors.TrainingNotFound;
        }

        var course = await db.TrainingCourses.AsNoTracking().FirstOrDefaultAsync(course => course.Id == request.CourseId, cancellationToken);
        if (course is null)
        {
            return SafetyErrors.TrainingCourseRequired;
        }

        var validUntil = request.ValidUntil ?? course.ValidUntil(request.CompletedOn);
        if (Check(request.CompletedOn, validUntil, request.Note) is { } error)
        {
            return error;
        }

        training.Change(course.Id, request.CompletedOn, validUntil, request.Note);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(training, course.Name, await IsLatestTrainingAsync(training, cancellationToken));
    }

    public async Task<Result> DeleteTrainingAsync(Guid id, CancellationToken cancellationToken)
    {
        var training = await db.EmployeeTrainings.FirstOrDefaultAsync(training => training.Id == id, cancellationToken);
        if (training is null)
        {
            return SafetyErrors.TrainingNotFound;
        }

        db.EmployeeTrainings.Remove(training);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    internal static MedicalExamResponse ToResponse(MedicalExam exam, bool isCurrent) =>
        new(exam.Id, exam.EmployeeId, (MedicalExamKind)exam.Type, exam.ExamDate, exam.ValidUntil, exam.Note, isCurrent);

    internal static EmployeeTrainingResponse ToResponse(EmployeeTraining training, string? courseName, bool isCurrent) =>
        new(training.Id, training.EmployeeId, training.CourseId, courseName ?? string.Empty, training.CompletedOn, training.ValidUntil, training.Note, isCurrent);

    private static AppError? Check(SaveMedicalExamRequest request)
    {
        if (!Enum.IsDefined((MedicalExamType)request.Kind))
        {
            return SafetyErrors.Invalid(new DomainException("Unknown medical exam type."));
        }

        if (request.ValidUntil < request.ExamDate)
        {
            return SafetyErrors.ExamExpiresBeforeDate;
        }

        return request.Note?.Trim().Length > MedicalExam.NoteMaxLength ? SafetyErrors.NoteTooLong : null;
    }

    private static AppError? Check(DateOnly completedOn, DateOnly? validUntil, string? note)
    {
        if (validUntil < completedOn)
        {
            return SafetyErrors.TrainingExpiresBeforeDate;
        }

        return note?.Trim().Length > EmployeeTraining.NoteMaxLength ? SafetyErrors.NoteTooLong : null;
    }

    private async Task<bool> IsLatestExamAsync(MedicalExam exam, CancellationToken cancellationToken) =>
        !await db.MedicalExams.AnyAsync(
            other => other.EmployeeId == exam.EmployeeId && other.Id != exam.Id
                && (other.ExamDate > exam.ExamDate || (other.ExamDate == exam.ExamDate && other.CreatedAt > exam.CreatedAt)),
            cancellationToken);

    private async Task<bool> IsLatestTrainingAsync(EmployeeTraining training, CancellationToken cancellationToken) =>
        !await db.EmployeeTrainings.AnyAsync(
            other => other.EmployeeId == training.EmployeeId && other.CourseId == training.CourseId && other.Id != training.Id
                && (other.CompletedOn > training.CompletedOn || (other.CompletedOn == training.CompletedOn && other.CreatedAt > training.CreatedAt)),
            cancellationToken);
}

/// <summary>The company's departments as child → parent, to tell which trainings concern a department through the ones above it.</summary>
internal sealed class DepartmentTree(Dictionary<Guid, Guid?> parents)
{
    public static async Task<DepartmentTree> LoadAsync(IAppDbContext db, CancellationToken cancellationToken) =>
        new(await db.Departments.AsNoTracking().ToDictionaryAsync(department => department.Id, department => department.ParentId, cancellationToken));

    /// <summary>The department and every one above it; empty for no department.</summary>
    public IReadOnlySet<Guid> WithAncestors(Guid? departmentId)
    {
        var chain = new HashSet<Guid>();
        for (var current = departmentId; current is { } id && chain.Add(id);)
        {
            current = parents.GetValueOrDefault(id);
        }

        return chain;
    }
}
