using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Absences;
using PersoNet.Domain.Absences;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Leave;

namespace PersoNet.Application.Absences;

/// <summary>
/// The register of absences that are not leave: sickness, unexcused and other absences, and work done elsewhere. HR records
/// them from a document; two absences of one employee never share a day.
/// </summary>
public sealed class AbsenceService(IAppDbContext db, ITenantContext tenant, TimeProvider time)
{
    /// <summary>Absences of the employee that touch the year (all of them without one), the latest first.</summary>
    public async Task<Result<IReadOnlyList<AbsenceResponse>>> ListAsync(Guid employeeId, int? year, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return AbsenceErrors.EmployeeNotFound;
        }

        var absences = db.Absences.AsNoTracking().Where(absence => absence.EmployeeId == employeeId);
        if (year is { } y)
        {
            if (y is < 2000 or > 2100)
            {
                return AbsenceErrors.YearOutOfRange;
            }

            var first = new DateOnly(y, 1, 1);
            var last = new DateOnly(y, 12, 31);
            absences = absences.Where(absence => absence.StartDate <= last && absence.EndDate >= first);
        }

        var rows = await absences.OrderByDescending(absence => absence.StartDate).ToListAsync(cancellationToken);
        return rows.Select(ToResponse).ToList();
    }

    public async Task<Result<AbsenceResponse>> CreateAsync(Guid employeeId, SaveAbsenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return AbsenceErrors.NoTenant;
        }

        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return AbsenceErrors.EmployeeNotFound;
        }

        if (Check(request) is { } formError)
        {
            return formError;
        }

        if (await OverlapsAsync(employeeId, null, request, cancellationToken))
        {
            return AbsenceErrors.Overlap;
        }

        Absence absence;
        try
        {
            absence = Absence.Create(tenantId, employeeId, (AbsenceType)request.Kind, request.StartDate, request.EndDate, request.Note, time.GetUtcNow());
        }
        catch (DomainException exception)
        {
            return AbsenceErrors.Invalid(exception);
        }

        db.Absences.Add(absence);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(absence);
    }

    public async Task<Result<AbsenceResponse>> UpdateAsync(Guid id, SaveAbsenceRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var absence = await db.Absences.FirstOrDefaultAsync(absence => absence.Id == id, cancellationToken);
        if (absence is null)
        {
            return AbsenceErrors.NotFound;
        }

        if (Check(request) is { } formError)
        {
            return formError;
        }

        if (await OverlapsAsync(absence.EmployeeId, absence.Id, request, cancellationToken))
        {
            return AbsenceErrors.Overlap;
        }

        try
        {
            absence.Change((AbsenceType)request.Kind, request.StartDate, request.EndDate, request.Note);
        }
        catch (DomainException exception)
        {
            return AbsenceErrors.Invalid(exception);
        }

        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(absence);
    }

    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var absence = await db.Absences.FirstOrDefaultAsync(absence => absence.Id == id, cancellationToken);
        if (absence is null)
        {
            return AbsenceErrors.NotFound;
        }

        db.Absences.Remove(absence);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    internal static AbsenceResponse ToResponse(Absence absence) => new(
        absence.Id,
        absence.EmployeeId,
        (AbsenceKind)absence.Type,
        absence.StartDate,
        absence.EndDate,
        absence.CalendarDays,
        PolishCalendar.WorkingDays(absence.StartDate, absence.EndDate),
        absence.Note);

    // The specific rule is reported with its own code, so the screen can show it next to the right field.
    private static AppError? Check(SaveAbsenceRequest request)
    {
        if (!Enum.IsDefined((AbsenceType)request.Kind))
        {
            return AbsenceErrors.Invalid(new DomainException("Unknown absence type."));
        }

        if (request.EndDate < request.StartDate)
        {
            return AbsenceErrors.EndBeforeStart;
        }

        return request.Note?.Trim().Length > Absence.NoteMaxLength ? AbsenceErrors.NoteTooLong : null;
    }

    private Task<bool> OverlapsAsync(Guid employeeId, Guid? except, SaveAbsenceRequest request, CancellationToken cancellationToken) =>
        db.Absences.AnyAsync(
            other => other.EmployeeId == employeeId && other.Id != except && other.StartDate <= request.EndDate && request.StartDate <= other.EndDate,
            cancellationToken);
}
