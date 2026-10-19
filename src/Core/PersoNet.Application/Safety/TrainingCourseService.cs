using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Safety;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Safety;

namespace PersoNet.Application.Safety;

/// <summary>
/// The company's list of trainings: what each is called, how long it is valid and for whom it is mandatory. A training
/// no longer run is deactivated rather than deleted, as employees' completed trainings refer to it.
/// </summary>
public sealed class TrainingCourseService(IAppDbContext db, ITenantContext tenant)
{
    public async Task<IReadOnlyList<TrainingCourseResponse>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var courses = await db.TrainingCourses.AsNoTracking()
            .Where(course => includeInactive || course.IsActive)
            .OrderBy(course => course.Name)
            .ToListAsync(cancellationToken);

        return courses.Select(ToResponse).ToList();
    }

    public async Task<Result<TrainingCourseResponse>> CreateAsync(SaveTrainingCourseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return SafetyErrors.NoTenant;
        }

        if (await CheckAsync(request, except: null, cancellationToken) is { } error)
        {
            return error;
        }

        TrainingCourse course;
        try
        {
            course = TrainingCourse.Create(tenantId, request.Name, request.ValidityMonths, request.IsMandatory, request.DepartmentIds, request.Description);
        }
        catch (DomainException exception)
        {
            return SafetyErrors.Invalid(exception);
        }

        db.TrainingCourses.Add(course);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(course);
    }

    public async Task<Result<TrainingCourseResponse>> UpdateAsync(Guid id, SaveTrainingCourseRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var course = await db.TrainingCourses.FirstOrDefaultAsync(course => course.Id == id, cancellationToken);
        if (course is null)
        {
            return SafetyErrors.CourseNotFound;
        }

        if (await CheckAsync(request, except: id, cancellationToken) is { } error)
        {
            return error;
        }

        try
        {
            course.Change(request.Name, request.ValidityMonths, request.IsMandatory, request.DepartmentIds ?? [], request.Description);
        }
        catch (DomainException exception)
        {
            return SafetyErrors.Invalid(exception);
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(course);
    }

    public async Task<Result<TrainingCourseResponse>> SetActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        var course = await db.TrainingCourses.FirstOrDefaultAsync(course => course.Id == id, cancellationToken);
        if (course is null)
        {
            return SafetyErrors.CourseNotFound;
        }

        if (active)
        {
            course.Activate();
        }
        else
        {
            course.Deactivate();
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(course);
    }

    internal static TrainingCourseResponse ToResponse(TrainingCourse course) => new(
        course.Id, course.Name, course.ValidityMonths, course.IsMandatory, course.DepartmentIds, course.Description, course.IsActive);

    // The specific rule is reported with its own code, so the dialog can show it next to the right field.
    private async Task<AppError?> CheckAsync(SaveTrainingCourseRequest request, Guid? except, CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim();
        if (string.IsNullOrEmpty(name))
        {
            return SafetyErrors.CourseNameRequired;
        }

        if (request.ValidityMonths is < 1 or > TrainingCourse.MaxValidityMonths)
        {
            return SafetyErrors.ValidityOutOfRange;
        }

        var normalized = name.ToUpperInvariant();
        if (await db.TrainingCourses.AnyAsync(course => course.NormalizedName == normalized && course.Id != except, cancellationToken))
        {
            return SafetyErrors.CourseNameTaken;
        }

        var departmentIds = (request.DepartmentIds ?? []).Distinct().ToList();
        var known = await db.Departments.CountAsync(department => departmentIds.Contains(department.Id), cancellationToken);
        return known != departmentIds.Count ? SafetyErrors.DepartmentNotFound : null;
    }
}
