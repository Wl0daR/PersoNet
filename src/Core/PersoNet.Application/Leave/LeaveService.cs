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
    public const int ChildCareDaysPerYear = 2;

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
        var days = PolishCalendar.WorkingDays(request.StartDate, request.EndDate);
        var year = request.StartDate.Year;

        var others = await db.LeaveRequests.AsNoTracking()
            .Where(other => other.EmployeeId == employee.Id
                && (other.Status == LeaveStatus.Pending || other.Status == LeaveStatus.Approved)
                && other.StartDate.Year == year)
            .ToListAsync(cancellationToken);

        if (others.Any(other => other.Overlaps(request.StartDate, request.EndDate)))
        {
            return LeaveErrors.Overlap;
        }

        if (await CheckLimitsAsync(employee.Id, type, days, year, others, cancellationToken) is { } limitError)
        {
            return limitError;
        }

        LeaveRequest leave;
        try
        {
            leave = LeaveRequest.Create(tenantId, employee.Id, type, request.StartDate, request.EndDate, request.Note, time.GetUtcNow());
        }
        catch (DomainException exception)
        {
            return LeaveErrors.Invalid(exception);
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

        return request.Note?.Trim().Length > LeaveRequest.NoteMaxLength ? LeaveErrors.NoteTooLong : null;
    }

    private async Task<AppError?> CheckLimitsAsync(Guid employeeId, LeaveType type, int days, int year, List<LeaveRequest> activeThisYear, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case LeaveType.OnDemand when activeThisYear.Where(other => other.Type == LeaveType.OnDemand).Sum(other => other.Days) + days > OnDemandDaysPerYear:
                return LeaveErrors.OnDemandLimit;
            case LeaveType.ChildCare when activeThisYear.Where(other => other.Type == LeaveType.ChildCare).Sum(other => other.Days) + days > ChildCareDaysPerYear:
                return LeaveErrors.ChildCareLimit;
        }

        if (!LeaveRequest.UsesAnnualLeaveType(type))
        {
            return null;
        }

        var balance = await BalanceAsync(employeeId, year, cancellationToken);
        if (!balance.HasEntitlement)
        {
            return LeaveErrors.NoEntitlement;
        }

        return days > balance.RemainingDays ? LeaveErrors.NotEnoughDays : null;
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
            .Select(request => new { request.Type, request.Status, request.Days })
            .ToListAsync(cancellationToken);

        var used = annual.Where(request => request.Status == LeaveStatus.Approved).Sum(request => request.Days);
        var pending = annual.Where(request => request.Status == LeaveStatus.Pending).Sum(request => request.Days);
        var onDemand = annual.Where(request => request.Type == LeaveType.OnDemand).Sum(request => request.Days);
        var total = entitlement?.Total ?? 0;

        return new LeaveBalanceResponse(
            employeeId,
            year,
            entitlement is not null,
            entitlement?.Days ?? 0,
            entitlement?.CarriedOverDays ?? 0,
            used,
            pending,
            total - used - pending,
            onDemand);
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
        request.Note,
        (LeaveState)request.Status,
        request.CreatedAt,
        request.DecidedAt,
        request.DecisionNote);
}
