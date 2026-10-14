using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Application.Company;
using PersoNet.Application.Leave;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.SelfService;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;

namespace PersoNet.Application.SelfService;

/// <summary>
/// The employee and manager portal: the signed-in person's own leave and, for managers, the requests of the employees
/// who report to them. The person is the current employee whose work e-mail is the account's e-mail; every rule of
/// leave itself stays in <see cref="LeaveService"/>.
/// </summary>
public sealed class PortalService(IAppDbContext db, ICurrentUser user, LeaveService leave, CompanyService company, TimeProvider time)
{
    public async Task<Result<MyProfileResponse>> GetProfileAsync(CancellationToken cancellationToken)
    {
        var me = await FindMeAsync(cancellationToken);
        if (!me.IsSuccess)
        {
            return me.Error!;
        }

        var employee = me.Value;
        var position = employee.PositionId is { } positionId
            ? await db.Positions.AsNoTracking().Where(p => p.Id == positionId).Select(p => p.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var department = employee.DepartmentId is { } departmentId
            ? await db.Departments.AsNoTracking().Where(d => d.Id == departmentId).Select(d => d.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        var manager = employee.ManagerId is { } managerId
            ? await db.Employees.AsNoTracking().Where(e => e.Id == managerId).Select(e => e.FirstName + " " + e.LastName).FirstOrDefaultAsync(cancellationToken)
            : null;
        var team = await TeamAsync(employee.Id, cancellationToken);

        return new MyProfileResponse(
            employee.Id, employee.Number, employee.FirstName, employee.LastName, employee.Email,
            position, department, manager, employee.HireDate, team.Count);
    }

    public async Task<Result<LeaveBalanceResponse>> GetBalanceAsync(int year, CancellationToken cancellationToken)
    {
        var me = await FindMeAsync(cancellationToken);
        return me.IsSuccess ? await leave.GetBalanceAsync(me.Value.Id, year, cancellationToken) : me.Error!;
    }

    public async Task<Result<List<LeaveRequestResponse>>> ListMyRequestsAsync(int? year, CancellationToken cancellationToken)
    {
        var me = await FindMeAsync(cancellationToken);
        if (!me.IsSuccess)
        {
            return me.Error!;
        }

        return (await leave.ListAsync(null, year, me.Value.Id, cancellationToken)).ToList();
    }

    public async Task<Result<LeaveRequestResponse>> CreateMyRequestAsync(MyLeaveRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var me = await FindMeAsync(cancellationToken);
        if (!me.IsSuccess)
        {
            return me.Error!;
        }

        // The company decides which kinds of leave go through the portal; the others are registered by HR.
        var allowed = await company.CheckPortalLeaveKindAsync(request.Kind, cancellationToken);
        return allowed.IsSuccess
            ? await leave.CreateAsync(new CreateLeaveRequest(me.Value.Id, request.Kind, request.StartDate, request.EndDate, request.Note, request.Hours), cancellationToken)
            : allowed.Error!;
    }

    /// <summary>The employee withdraws their own request while it still waits for a decision.</summary>
    public async Task<Result<LeaveRequestResponse>> WithdrawMyRequestAsync(Guid id, CancellationToken cancellationToken)
    {
        var me = await FindMeAsync(cancellationToken);
        if (!me.IsSuccess)
        {
            return me.Error!;
        }

        var request = await leave.GetAsync(id, cancellationToken);
        if (!request.IsSuccess || request.Value.EmployeeId != me.Value.Id)
        {
            return SelfServiceErrors.RequestNotFound;
        }

        return request.Value.State != LeaveState.Pending
            ? SelfServiceErrors.OnlyPendingCanBeWithdrawn
            : await leave.CancelAsync(id, new LeaveDecisionRequest("Wycofany przez pracownika."), cancellationToken);
    }

    /// <summary>
    /// The current employees who report to the signed-in manager, by name, with their annual leave in
    /// <paramref name="year"/> and the leave they are on today or take next (approved or waiting for a decision).
    /// </summary>
    public async Task<Result<List<TeamMemberResponse>>> ListTeamAsync(int year, CancellationToken cancellationToken)
    {
        var me = await FindMeAsync(cancellationToken);
        if (!me.IsSuccess)
        {
            return me.Error!;
        }

        var today = Today;
        var managerId = me.Value.Id;
        var members = await db.Employees.AsNoTracking()
            .Where(employee => employee.ManagerId == managerId && (employee.TerminationDate == null || employee.TerminationDate >= today))
            .OrderBy(employee => employee.LastName).ThenBy(employee => employee.FirstName)
            .Select(employee => new
            {
                employee.Id,
                employee.Number,
                employee.FirstName,
                employee.LastName,
                employee.Email,
                Position = db.Positions.Where(p => p.Id == employee.PositionId).Select(p => p.Name).FirstOrDefault(),
                Department = db.Departments.Where(d => d.Id == employee.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                employee.HireDate,
            })
            .ToListAsync(cancellationToken);

        var ids = members.Select(member => member.Id).ToList();
        var upcoming = await db.LeaveRequests.AsNoTracking()
            .Where(request => ids.Contains(request.EmployeeId) && request.EndDate >= today
                && (request.Status == LeaveStatus.Approved || request.Status == LeaveStatus.Pending))
            .OrderBy(request => request.StartDate)
            .Select(request => new { request.EmployeeId, request.Type, request.StartDate, request.EndDate, request.Status })
            .ToListAsync(cancellationToken);

        var team = new List<TeamMemberResponse>(members.Count);
        foreach (var member in members)
        {
            var balance = await leave.GetBalanceAsync(member.Id, year, cancellationToken);
            if (!balance.IsSuccess)
            {
                return balance.Error!;
            }

            var leaves = upcoming.Where(request => request.EmployeeId == member.Id)
                .Select(request => new TeamMemberLeave((LeaveKind)request.Type, request.StartDate, request.EndDate, (LeaveState)request.Status))
                .ToList();

            team.Add(new TeamMemberResponse(
                member.Id, member.Number, member.FirstName, member.LastName, member.Email, member.Position, member.Department, member.HireDate,
                balance.Value,
                leaves.FirstOrDefault(l => l.State == LeaveState.Approved && l.StartDate <= today),
                leaves.FirstOrDefault(l => l.StartDate > today)));
        }

        return team;
    }

    /// <summary>Requests of the employees who report to the signed-in manager, waiting ones first.</summary>
    public async Task<Result<List<LeaveRequestResponse>>> ListTeamRequestsAsync(LeaveState? state, int? year, CancellationToken cancellationToken)
    {
        var me = await FindMeAsync(cancellationToken);
        if (!me.IsSuccess)
        {
            return me.Error!;
        }

        var team = await TeamAsync(me.Value.Id, cancellationToken);
        if (team.Count == 0)
        {
            return new List<LeaveRequestResponse>();
        }

        return (await leave.ListForEmployeesAsync(state, year, team, cancellationToken)).ToList();
    }

    public Task<Result<LeaveRequestResponse>> ApproveTeamRequestAsync(Guid id, LeaveDecisionRequest decision, CancellationToken cancellationToken) =>
        DecideForTeamAsync(id, () => leave.ApproveAsync(id, decision, cancellationToken), cancellationToken);

    public Task<Result<LeaveRequestResponse>> RejectTeamRequestAsync(Guid id, LeaveDecisionRequest decision, CancellationToken cancellationToken) =>
        DecideForTeamAsync(id, () => leave.RejectAsync(id, decision, cancellationToken), cancellationToken);

    // A manager decides only for the people who report to them; anyone else's request looks like it does not exist.
    private async Task<Result<LeaveRequestResponse>> DecideForTeamAsync(
        Guid id, Func<Task<Result<LeaveRequestResponse>>> decide, CancellationToken cancellationToken)
    {
        var me = await FindMeAsync(cancellationToken);
        if (!me.IsSuccess)
        {
            return me.Error!;
        }

        var request = await leave.GetAsync(id, cancellationToken);
        if (!request.IsSuccess || !(await TeamAsync(me.Value.Id, cancellationToken)).Contains(request.Value.EmployeeId))
        {
            return SelfServiceErrors.RequestNotFound;
        }

        return await decide();
    }

    private async Task<Result<Employee>> FindMeAsync(CancellationToken cancellationToken)
    {
        var email = user.Email?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
        {
            return SelfServiceErrors.NotLinked;
        }

        var today = Today;

        // Runs in the database as lower(email) = @email; culture and string.Equals overloads do not translate to SQL.
#pragma warning disable CA1304, CA1311, CA1862
        var matches = await db.Employees.AsNoTracking()
            .Where(employee => employee.Email != null && employee.Email.ToLower() == email
                && (employee.TerminationDate == null || employee.TerminationDate >= today))
            .OrderBy(employee => employee.Id)
            .Take(2)
            .ToListAsync(cancellationToken);
#pragma warning restore CA1304, CA1311, CA1862

        return matches.Count switch
        {
            0 => SelfServiceErrors.NotLinked,
            1 => matches[0],
            _ => SelfServiceErrors.Ambiguous,
        };
    }

    private async Task<List<Guid>> TeamAsync(Guid managerId, CancellationToken cancellationToken)
    {
        var today = Today;
        return await db.Employees.AsNoTracking()
            .Where(employee => employee.ManagerId == managerId && (employee.TerminationDate == null || employee.TerminationDate >= today))
            .Select(employee => employee.Id)
            .ToListAsync(cancellationToken);
    }

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);
}
