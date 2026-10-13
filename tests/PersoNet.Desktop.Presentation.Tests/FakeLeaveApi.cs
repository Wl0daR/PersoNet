using System.Net;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Leave;
using PersoNet.Contracts.Leave;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>In-memory stand-in for the leave endpoints, with the rules the screens react to.</summary>
internal sealed class FakeLeaveApi : ILeaveApi
{
    public List<LeaveRequestResponse> Requests { get; } = [];

    public Dictionary<(Guid EmployeeId, int Year), SaveLeaveEntitlementRequest> Entitlements { get; } = [];

    public Task<IReadOnlyList<LeaveRequestResponse>> ListAsync(LeaveState? state, int? year, Guid? employeeId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<LeaveRequestResponse>>(Requests
            .Where(request => state is null || request.State == state)
            .Where(request => year is null || request.StartDate.Year == year)
            .Where(request => employeeId is null || request.EmployeeId == employeeId)
            .OrderBy(request => request.State == LeaveState.Pending ? 0 : 1)
            .ThenByDescending(request => request.StartDate)
            .ToList());

    public Task<LeaveRequestResponse> CreateAsync(CreateLeaveRequest request, CancellationToken cancellationToken)
    {
        var days = 0;
        for (var day = request.StartDate; day <= request.EndDate; day = day.AddDays(1))
        {
            days += day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday ? 0 : 1;
        }

        if (request.Kind == LeaveKind.Annual && !Entitlements.ContainsKey((request.EmployeeId, request.StartDate.Year)))
        {
            throw new ApiException(HttpStatusCode.BadRequest, "Najpierw ustaw wymiar urlopu pracownika na ten rok.", "leave.no_entitlement");
        }

        var created = new LeaveRequestResponse(
            Guid.CreateVersion7(), request.EmployeeId, "P001", "Nowak Jan", request.Kind, request.StartDate, request.EndDate, days, request.Note,
            LeaveState.Pending, DateTimeOffset.UtcNow, null, null);
        Requests.Add(created);
        return Task.FromResult(created);
    }

    public Task<LeaveRequestResponse> ApproveAsync(Guid id, string? note, CancellationToken cancellationToken) => Decide(id, LeaveState.Approved, note);

    public Task<LeaveRequestResponse> RejectAsync(Guid id, string reason, CancellationToken cancellationToken) => Decide(id, LeaveState.Rejected, reason);

    public Task<LeaveRequestResponse> CancelAsync(Guid id, string? note, CancellationToken cancellationToken) => Decide(id, LeaveState.Cancelled, note);

    public Task<LeaveBalanceResponse> GetBalanceAsync(Guid employeeId, int year, CancellationToken cancellationToken)
    {
        var has = Entitlements.TryGetValue((employeeId, year), out var entitlement);
        var annual = Requests.Where(request => request.EmployeeId == employeeId && request.StartDate.Year == year
            && request.Kind is LeaveKind.Annual or LeaveKind.OnDemand).ToList();
        var used = annual.Where(request => request.State == LeaveState.Approved).Sum(request => request.Days);
        var pending = annual.Where(request => request.State == LeaveState.Pending).Sum(request => request.Days);
        var total = has ? entitlement!.Days + entitlement.CarriedOverDays : 0;

        return Task.FromResult(new LeaveBalanceResponse(
            employeeId, year, has, entitlement?.Days ?? 0, entitlement?.CarriedOverDays ?? 0, used, pending, total - used - pending, 0));
    }

    public Task<LeaveBalanceResponse> SetEntitlementAsync(Guid employeeId, int year, SaveLeaveEntitlementRequest request, CancellationToken cancellationToken)
    {
        Entitlements[(employeeId, year)] = request;
        return GetBalanceAsync(employeeId, year, cancellationToken);
    }

    public LeaveRequestResponse Add(Guid employeeId, DateOnly start, DateOnly end, LeaveState state = LeaveState.Pending)
    {
        var request = new LeaveRequestResponse(
            Guid.CreateVersion7(), employeeId, "P001", "Nowak Jan", LeaveKind.Annual, start, end, 1, null, state, DateTimeOffset.UtcNow, null, null);
        Requests.Add(request);
        return request;
    }

    private Task<LeaveRequestResponse> Decide(Guid id, LeaveState state, string? note)
    {
        var index = Requests.FindIndex(request => request.Id == id);
        if (Requests[index].State != LeaveState.Pending && state != LeaveState.Cancelled)
        {
            throw new ApiException(HttpStatusCode.Conflict, "Ten wniosek został już rozpatrzony.", "leave.already_decided");
        }

        Requests[index] = Requests[index] with { State = state, DecisionNote = note, DecidedAt = DateTimeOffset.UtcNow };
        return Task.FromResult(Requests[index]);
    }
}
