using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Absences;
using PersoNet.Contracts.Safety;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Safety;

namespace PersoNet.Application.Safety;

/// <summary>
/// Moves the list of trainings, the trainings employees completed and their medical examinations from an earlier system,
/// matched by personnel number and training name. What is already in PersoNet is left alone, so the same files can be
/// imported again.
/// </summary>
public sealed class SafetyImportService(IAppDbContext db, ITenantContext tenant, TimeProvider time)
{
    public const int MaxRows = 5000;

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public async Task<Result<ImportSafetyResponse>> ImportAsync(ImportSafetyRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return SafetyErrors.NoTenant;
        }

        var courseRows = request.Courses ?? [];
        var trainingRows = request.Trainings ?? [];
        var examRows = request.Exams ?? [];
        if (courseRows.Count + trainingRows.Count + examRows.Count == 0)
        {
            return SafetyErrors.ImportEmpty;
        }

        if (courseRows.Count > MaxRows || trainingRows.Count > MaxRows || examRows.Count > MaxRows)
        {
            return SafetyErrors.ImportTooLarge;
        }

        var problems = new List<ImportLeaveProblem>();
        var now = time.GetUtcNow();
        int coursesAdded = 0, trainingsAdded = 0, examsAdded = 0, existing = 0;

        var courses = await db.TrainingCourses.AsNoTracking()
            .ToDictionaryAsync(course => course.NormalizedName, course => (course.Id, course.ValidityMonths), StringComparer.Ordinal, cancellationToken);
        if (courseRows.Count > 0)
        {
            var departments = await db.Departments.AsNoTracking()
                .ToDictionaryAsync(department => department.Code.ToUpperInvariant(), department => department.Id, StringComparer.Ordinal, cancellationToken);
            foreach (var row in courseRows)
            {
                var name = row.Name?.Trim() ?? string.Empty;
                if (courses.ContainsKey(name.ToUpperInvariant()))
                {
                    existing++;
                    continue;
                }

                try
                {
                    // A department the import of the structure skipped only narrows the training down less.
                    var departmentIds = (row.DepartmentCodes ?? []).Select(code => departments.GetValueOrDefault(code.Trim().ToUpperInvariant())).Where(id => id != Guid.Empty);
                    var course = TrainingCourse.Create(tenantId, name, row.ValidityMonths, row.IsMandatory, departmentIds, row.Description);
                    db.TrainingCourses.Add(course);
                    courses[course.NormalizedName] = (course.Id, course.ValidityMonths);
                    coursesAdded++;
                }
                catch (DomainException)
                {
                    problems.Add(new ImportLeaveProblem(row.Line, string.Empty, $"Szkolenie „{name}” ma nieprawidłową nazwę, ważność ({row.ValidityMonths} mies.) lub opis."));
                }
            }
        }

        var numbers = trainingRows.Select(row => row.Number).Concat(examRows.Select(row => row.Number)).Select(Number).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(employee => numbers.Contains(employee.Number))
            .ToDictionaryAsync(employee => employee.Number, employee => employee.Id, StringComparer.Ordinal, cancellationToken);
        var ids = employees.Values.ToList();

        var trained = (await db.EmployeeTrainings.AsNoTracking()
                .Where(training => ids.Contains(training.EmployeeId))
                .Select(training => new { training.EmployeeId, training.CourseId, training.CompletedOn })
                .ToListAsync(cancellationToken))
            .Select(training => (training.EmployeeId, training.CourseId, training.CompletedOn))
            .ToHashSet();
        foreach (var row in trainingRows)
        {
            var number = Number(row.Number);
            if (!employees.TryGetValue(number, out var employeeId))
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, NoEmployee(number)));
                continue;
            }

            if (!courses.TryGetValue(row.CourseName?.Trim().ToUpperInvariant() ?? string.Empty, out var course))
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, $"Nie ma szkolenia „{row.CourseName}” na liście szkoleń."));
                continue;
            }

            if (!trained.Add((employeeId, course.Id, row.CompletedOn)))
            {
                existing++;
                continue;
            }

            try
            {
                var validUntil = row.ValidUntil ?? (course.ValidityMonths is { } months ? row.CompletedOn.AddMonths(months) : null);
                db.EmployeeTrainings.Add(EmployeeTraining.Create(tenantId, employeeId, course.Id, row.CompletedOn, validUntil, row.Note, now));
                trainingsAdded++;
            }
            catch (DomainException)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, $"Szkolenie z {Day(row.CompletedOn)} traci ważność przed dniem ukończenia ({Day(row.ValidUntil)})."));
            }
        }

        var examined = (await db.MedicalExams.AsNoTracking()
                .Where(exam => ids.Contains(exam.EmployeeId))
                .Select(exam => new { exam.EmployeeId, exam.ExamDate })
                .ToListAsync(cancellationToken))
            .Select(exam => (exam.EmployeeId, exam.ExamDate))
            .ToHashSet();
        foreach (var row in examRows)
        {
            var number = Number(row.Number);
            if (!employees.TryGetValue(number, out var employeeId))
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, NoEmployee(number)));
                continue;
            }

            if (!examined.Add((employeeId, row.ExamDate)))
            {
                existing++;
                continue;
            }

            try
            {
                db.MedicalExams.Add(MedicalExam.Create(tenantId, employeeId, (MedicalExamType)row.Kind, row.ExamDate, row.ValidUntil, row.Note, now));
                examsAdded++;
            }
            catch (DomainException)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, $"Badanie z {Day(row.ExamDate)} traci ważność przed dniem badania ({Day(row.ValidUntil)})."));
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportSafetyResponse(coursesAdded, trainingsAdded, examsAdded, existing, problems);
    }

    private static string NoEmployee(string number) => $"Nie ma pracownika o numerze {number}; najpierw zaimportuj pracowników.";

    private static string Day(DateOnly? day) => day?.ToString("d", Polish) ?? "–";

    private static string Number(string? number) => number?.Trim().ToUpperInvariant() ?? string.Empty;
}
