using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Leave;
using PersoNet.Contracts.Leave;

namespace PersoNet.Api.Leave;

/// <summary>
/// Leave requests with approval, annual leave entitlements and balances. Reading needs <see cref="Permissions.LeaveRead"/>,
/// registering and deciding needs <see cref="Permissions.LeaveManage"/>.
/// </summary>
internal static class LeaveEndpoints
{
    public static RouteGroupBuilder MapLeaveEndpoints(this RouteGroupBuilder api)
    {
        var requests = api.MapGroup("/leave-requests").WithTags("Urlopy");

        requests.MapGet("/", (LeaveState? state, int? year, Guid? employeeId, LeaveService service, CancellationToken cancellationToken) =>
                service.ListAsync(state, year, employeeId, cancellationToken))
            .RequireAuthorization(Permissions.LeaveRead)
            .WithName("ListLeaveRequests").WithSummary("Wnioski urlopowe (najpierw oczekujące), z filtrem stanu, roku i pracownika");

        requests.MapGet("/{id:guid}", async (Guid id, LeaveService service, CancellationToken cancellationToken) =>
                (await service.GetAsync(id, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.LeaveRead)
            .WithName("GetLeaveRequest").WithSummary("Wniosek urlopowy");

        requests.MapPost("/", async (CreateLeaveRequest request, LeaveService service, CancellationToken cancellationToken) =>
            {
                var result = await service.CreateAsync(request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/leave-requests/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Permissions.LeaveManage)
            .WithName("CreateLeaveRequest").WithSummary("Złóż wniosek urlopowy w imieniu pracownika");

        requests.MapPost("/{id:guid}/approve", async (Guid id, LeaveDecisionRequest decision, LeaveService service, CancellationToken cancellationToken) =>
                (await service.ApproveAsync(id, decision, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.LeaveManage)
            .WithName("ApproveLeaveRequest").WithSummary("Zatwierdź wniosek");

        requests.MapPost("/{id:guid}/reject", async (Guid id, LeaveDecisionRequest decision, LeaveService service, CancellationToken cancellationToken) =>
                (await service.RejectAsync(id, decision, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.LeaveManage)
            .WithName("RejectLeaveRequest").WithSummary("Odrzuć wniosek (z powodem)");

        requests.MapPost("/{id:guid}/cancel", async (Guid id, LeaveDecisionRequest decision, LeaveService service, CancellationToken cancellationToken) =>
                (await service.CancelAsync(id, decision, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.LeaveManage)
            .WithName("CancelLeaveRequest").WithSummary("Anuluj oczekujący lub zatwierdzony urlop");

        var employee = api.MapGroup("/employees/{employeeId:guid}/leave/{year:int}").WithTags("Urlopy");

        employee.MapGet("/", async (Guid employeeId, int year, LeaveService service, CancellationToken cancellationToken) =>
                (await service.GetBalanceAsync(employeeId, year, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.LeaveRead)
            .WithName("GetLeaveBalance").WithSummary("Wymiar i saldo urlopu wypoczynkowego pracownika w roku");

        employee.MapPut("/", async (Guid employeeId, int year, SaveLeaveEntitlementRequest request, LeaveService service, CancellationToken cancellationToken) =>
                (await service.SetEntitlementAsync(employeeId, year, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.LeaveManage)
            .WithName("SetLeaveEntitlement").WithSummary("Ustaw wymiar urlopu na rok (i dni zaległe)");

        return api;
    }
}
