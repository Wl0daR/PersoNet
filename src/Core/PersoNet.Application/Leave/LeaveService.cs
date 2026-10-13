using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Leave;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;

namespace PersoNet.Application.Leave;

/// <summary>
/// Leave requests and their approval, annual leave entitlements and balances. Limits that depend on other requests
/// (balance, on-demand and child-care caps, overlaps) are checked here; rules of a single request live in the entity.
/// </summary>
public sealed class LeaveService(IAppDbContext db, ITenantContext tenant, ICurrentUser user, TimeProvider time)
{
    public const int OnDemandDaysPerYear = 4;

    /// <summary>Child care (art. 188) and force majeure (art. 148¹): 2 days or 16 hours a year each, as the employee picks first.</summary>
    public const int ReleaseDaysPerYear = 2;
    public const int ReleaseHoursPerYear = 16;

    public async Task<IReadOnlyList<LeaveRequestResponse>> ListAsync(LeaveState? state, int? year, Guid? employeeId, CancellationToken cancellationToken)
    {
        var requests = db.LeaveRequests.AsNoTracking();

        if (state is { } wanted)
        {
            var status = (LeaveStatus)wanted;
            requests = requests.Where(request => request.Status == status);
        }

        if (year is { } y)
        {
            requests = requests.Where(request => request.StartDate.Year == y);
        }

        if (employeeId is { } id)
        {
            requests = requests.Where(request => request.EmployeeId == id);
        }

        var rows = await requests
            .Join(db.Employees, request => request.EmployeeId, employee => employee.Id, (request, employee) => new { request, employee })
            .OrderBy(row => row.request.Status == LeaveStatus.Pending ? 0 : 1)
            .ThenByDescending(row => row.request.StartDate)
            .ToListAsync(cancellationToken);

        return rows.Select(row => ToResponse(row.request, row.employee)).ToList();
    }

    public async Task<Result<LeaveRequestResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var request = await db.LeaveRequests.AsNoTracking().FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
        if (request is null)
        {
            return LeaveErrors.NotFound;
        }

        var employee = await db.Employees.AsNoTracking().FirstAsync(employee => employee.Id == request.EmployeeId, cancellationToken);
        return ToResponse(request, employee);
    }

    public async Task<Result<LeaveRequestResponse>> CreateAsync(CreateLeaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return LeaveErrors.NoTenant;
        }

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.Id == request.EmployeeId, cancellationToken);
        if (employee is null)
        {
            return LeaveErrors.EmployeeNotFound;
        }

        if (Check(request) is { } formError)
        {
            return formError;
        }

        if (request.StartDate < employee.HireDate || (employee.TerminationDate is { } lastDay && request.EndDate > lastDay))
        {
            return LeaveErrors.OutsideEmployment;
        }

        var type = (LeaveType)request.Kind;
        var year = request.StartDate.Year;
        var dailyHours = await DailyHoursAsync(employee.Id, request.StartDate, cancellationToken);

        if (request.Hours is { } asked && !LeaveRequest.IsValidHours(asked, dailyHours))
        {
            return LeaveErrors.HoursInvalid(dailyHours);
        }

        var others = await db.LeaveRequests.AsNoTracking()
            .Where(other => other.EmployeeId == employee.Id
                && (other.Status == LeaveStatus.Pending || other.Status == LeaveStatus.Approved)
                && other.StartDate.Year == year)
            .ToListAsync(cancellationToken);

        if (others.Any(other => other.Overlaps(request.StartDate, request.EndDate)))
        {
            return LeaveErrors.Overlap;
        }

        LeaveRequest leave;
        try
        {
            leave = LeaveRequest.Create(tenantId, employee.Id, type, request.StartDate, request.EndDate, dailyHours, request.Hours, request.Note, time.GetUtcNow());
        }
        catch (DomainException exception)
        {
            return LeaveErrors.Invalid(exception);
        }

        if (await CheckLimitsAsync(leave, dailyHours, others, cancellationToken) is { } limitError)
        {
            return limitError;
        }

        db.LeaveRequests.Add(leave);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(leave, employee);
    }

    public Task<Result<LeaveRequestResponse>> ApproveAsync(Guid id, LeaveDecisionRequest decision, CancellationToken cancellationToken) =>
        DecideAsync(id, decision, LeaveStatus.Approved, cancellationToken);

    public Task<Result<LeaveRequestResponse>> RejectAsync(Guid id, LeaveDecisionRequest decision, CancellationToken cancellationToken) =>
        DecideAsync(id, decision, LeaveStatus.Rejected, cancellationToken);

    public Task<Result<LeaveRequestResponse>> CancelAsync(Guid id, LeaveDecisionRequest decision, CancellationToken cancellationToken) =>
        DecideAsync(id, decision, LeaveStatus.Cancelled, cancellationToken);

    /// <summary>Public holidays of a year, for calendars.</summary>
    public static Result<IReadOnlyList<HolidayResponse>> Holidays(int year)
    {
        if (year is < 2000 or > 2100)
        {
            return LeaveErrors.YearOutOfRange;
        }

        return PolishCalendar.Holidays(year).Select(holiday => new HolidayResponse(holiday.Date, holiday.Name)).ToList();
    }

    public async Task<Result<LeaveBalanceResponse>> GetBalanceAsync(Guid employeeId, int year, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return LeaveErrors.EmployeeNotFound;
        }

        return await BalanceAsync(employeeId, year, cancellationToken);
    }

    public async Task<Result<LeaveBalanceResponse>> SetEntitlementAsync(Guid employeeId, int year, SaveLeaveEntitlementRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return LeaveErrors.NoTenant;
        }

        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return LeaveErrors.EmployeeNotFound;
        }

        if (request.Days is < 0 or > LeaveEntitlement.MaxDays || request.CarriedOverDays is < 0 or > LeaveEntitlement.MaxDays || year is < 2000 or > 2100)
        {
            return LeaveErrors.EntitlementInvalid;
        }

        var entitlement = await db.LeaveEntitlements.FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.Year == year, cancellationToken);
        if (entitlement is null)
        {
            db.LeaveEntitlements.Add(LeaveEntitlement.Create(tenantId, employeeId, year, request.Days, request.CarriedOverDays));
        }
        else
        {
            entitlement.Change(request.Days, request.CarriedOverDays);
        }

        await db.SaveChangesAsync(cancellationToken);

        return await BalanceAsync(employeeId, year, cancellationToken);
    }

    private async Task<Result<LeaveRequestResponse>> DecideAsync(Guid id, LeaveDecisionRequest decision, LeaveStatus outcome, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(decision);

        var leave = await db.LeaveRequests.FirstOrDefaultAsync(request => request.Id == id, cancellationToken);
        if (leave is null)
        {
            return LeaveErrors.NotFound;
        }

        if (decision.Note?.Trim().Length > LeaveRequest.NoteMaxLength)
        {
            return LeaveErrors.NoteTooLong;
        }

        var now = time.GetUtcNow();
        switch (outcome)
        {
            case LeaveStatus.Approved or LeaveStatus.Rejected when leave.Status != LeaveStatus.Pending:
                return LeaveErrors.AlreadyDecided;
            case LeaveStatus.Rejected when string.IsNullOrWhiteSpace(decision.Note):
                return LeaveErrors.ReasonRequired;
            case LeaveStatus.Cancelled when !leave.IsActive:
                return LeaveErrors.NotActive;
            case LeaveStatus.Approved:
                leave.Approve(user.UserId, now, decision.Note);
                break;
            case LeaveStatus.Rejected:
                leave.Reject(user.UserId, now, decision.Note!);
                break;
            default:
                leave.Cancel(user.UserId, now, decision.Note);
                break;
        }

        await db.SaveChangesAsync(cancellationToken);

        var employee = await db.Employees.AsNoTracking().FirstAsync(employee => employee.Id == leave.EmployeeId, cancellationToken);
        return ToResponse(leave, employee);
    }

    // The specific rule is reported with its own code, so the screen can show it next to the right field.
    private static AppError? Check(CreateLeaveRequest request)
    {
        if (!Enum.IsDefined((LeaveType)request.Kind))
        {
            return LeaveErrors.Invalid(new DomainException("Unknown leave type."));
        }

        if (request.EndDate < request.StartDate)
        {
            return LeaveErrors.EndBeforeStart;
        }

        if (request.StartDate.Year != request.EndDate.Year)
        {
            return LeaveErrors.CrossesYear;
        }

        if (PolishCalendar.WorkingDays(request.StartDate, request.EndDate) == 0)
        {
            return LeaveErrors.NoWorkingDays;
        }

        if (request.Hours is not null && !LeaveRequest.AllowsHoursType((LeaveType)request.Kind))
        {
            return LeaveErrors.HoursNotAllowed;
        }

        if (request.Hours is not null && request.StartDate != request.EndDate)
        {
            return LeaveErrors.HoursOneDay;
        }

        return request.Note?.Trim().Length > LeaveRequest.NoteMaxLength ? LeaveErrors.NoteTooLong : null;
    }

    private async Task<AppError?> CheckLimitsAsync(LeaveRequest leave, decimal dailyHours, List<LeaveRequest> activeThisYear, CancellationToken cancellationToken)
    {
        switch (leave.Type)
        {
            case LeaveType.OnDemand when activeThisYear.Where(other => other.Type == LeaveType.OnDemand).Sum(other => other.Days) + leave.Days > OnDemandDaysPerYear:
                return LeaveErrors.OnDemandLimit;
            case LeaveType.ChildCare or LeaveType.ForceMajeure:
                return CheckRelease(leave, dailyHours, activeThisYear);
            case LeaveType.Annual or LeaveType.OnDemand:
                break;
            default:
                return null;
        }

        var balance = await BalanceAsync(leave.EmployeeId, leave.StartDate.Year, cancellationToken);
        if (!balance.HasEntitlement)
        {
            return LeaveErrors.NoEntitlement;
        }

        // Annual leave in hours only for what is left when that is less than a working day (art. 154² § 2 KP).
        if (leave.IsHourly && balance.RemainingHours >= dailyHours)
        {
            return LeaveErrors.HoursOnlyForRemainder;
        }

        return leave.Hours > balance.RemainingHours ? LeaveErrors.NotEnoughDays : null;
    }

    // Child care and force majeure: in days or in hours, as the first request of the year chose; hours are proportional
    // to part-time work.
    private static AppError? CheckRelease(LeaveRequest leave, decimal dailyHours, List<LeaveRequest> activeThisYear)
    {
        var earlier = activeThisYear.Where(other => other.Type == leave.Type).ToList();
        if (earlier.Count > 0 && earlier[0].IsHourly != leave.IsHourly)
        {
            return LeaveErrors.DaysOrHours(earlier[0].IsHourly);
        }

        var tooMuch = leave.IsHourly
            ? earlier.Sum(other => other.Hours) + leave.Hours > ReleaseHoursPerYear * dailyHours / LeaveEntitlement.HoursPerDay
            : earlier.Sum(other => other.Days) + leave.Days > ReleaseDaysPerYear;

        return !tooMuch ? null : leave.Type == LeaveType.ChildCare ? LeaveErrors.ChildCareLimit : LeaveErrors.ForceMajeureLimit;
    }

    /// <summary>The employee's working day on <paramref name="day"/>: 8 hours times the working time of the contract then.</summary>
    private async Task<decimal> DailyHoursAsync(Guid employeeId, DateOnly day, CancellationToken cancellationToken)
    {
        var workingTime = await db.Contracts.AsNoTracking()
            .Where(contract => contract.EmployeeId == employeeId && contract.StartDate <= day && (contract.EndDate == null || contract.EndDate >= day))
            .OrderByDescending(contract => contract.StartDate)
            .Select(contract => contract.WorkingTime)
            .FirstOrDefaultAsync(cancellationToken);

        return Math.Round(LeaveEntitlement.HoursPerDay * (workingTime ?? 1m), 2);
    }

    private async Task<LeaveBalanceResponse> BalanceAsync(Guid employeeId, int year, CancellationToken cancellationToken)
    {
        var entitlement = await db.LeaveEntitlements.AsNoTracking()
            .FirstOrDefaultAsync(e => e.EmployeeId == employeeId && e.Year == year, cancellationToken);

        var annual = await db.LeaveRequests.AsNoTracking()
            .Where(request => request.EmployeeId == employeeId
                && request.StartDate.Year == year
                && (request.Type == LeaveType.Annual || request.Type == LeaveType.OnDemand)
                && (request.Status == LeaveStatus.Pending || request.Status == LeaveStatus.Approved))
            .Select(request => new { request.Type, request.Status, request.Days, request.Hours })
            .ToListAsync(cancellationToken);

        var used = annual.Where(request => request.Status == LeaveStatus.Approved).Sum(request => request.Hours);
        var pending = annual.Where(request => request.Status == LeaveStatus.Pending).Sum(request => request.Hours);
        var onDemand = annual.Where(request => request.Type == LeaveType.OnDemand).Sum(request => request.Days);
        var total = entitlement?.TotalHours ?? 0;

        // The working day today, or on the nearest day of that year.
        var today = DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime);
        var day = today.Year == year ? today : new DateOnly(year, today.Year < year ? 1 : 12, today.Year < year ? 1 : 31);

        return new LeaveBalanceResponse(
            employeeId,
            year,
            entitlement is not null,
            entitlement?.Days ?? 0,
            entitlement?.CarriedOverDays ?? 0,
            total,
            used,
            pending,
            total - used - pending,
            onDemand,
            await DailyHoursAsync(employeeId, day, cancellationToken));
    }

    private static LeaveRequestResponse ToResponse(LeaveRequest request, Employee employee) => new(
        request.Id,
        request.EmployeeId,
        employee.Number,
        $"{employee.LastName} {employee.FirstName}",
        (LeaveKind)request.Type,
        request.StartDate,
        request.EndDate,
        request.Days,
        request.Hours,
        request.IsHourly,
        request.Note,
        (LeaveState)request.Status,
        request.CreatedAt,
        request.DecidedAt,
        request.DecisionNote);
}
