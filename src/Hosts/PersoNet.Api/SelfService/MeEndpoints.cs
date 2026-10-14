using PersoNet.Api.Common;
using PersoNet.Application.SelfService;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.SelfService;

namespace PersoNet.Api.SelfService;

/// <summary>
/// The employee and manager portal. Any signed-in person may call these; each call only reaches the caller's own
/// record (found by the account's e-mail) and, for managers, the people who report to them.
/// </summary>
internal static class MeEndpoints
{
    public static RouteGroupBuilder MapMeEndpoints(this RouteGroupBuilder api)
    {
        var me = api.MapGroup("/me").WithTags("Portal pracownika");

        me.MapGet("/employee", async (PortalService portal, CancellationToken cancellationToken) =>
                (await portal.GetProfileAsync(cancellationToken)).ToOk())
            .WithName("GetMyProfile").WithSummary("Moja kartoteka: stanowisko, dział, przełożony i liczba osób w zespole");

        me.MapGet("/leave/{year:int}", async (int year, PortalService portal, CancellationToken cancellationToken) =>
                (await portal.GetBalanceAsync(year, cancellationToken)).ToOk())
            .WithName("GetMyLeaveBalance").WithSummary("Mój urlop wypoczynkowy w roku");

        me.MapGet("/leave-requests", async (int? year, PortalService portal, CancellationToken cancellationToken) =>
                (await portal.ListMyRequestsAsync(year, cancellationToken)).ToOk())
            .WithName("ListMyLeaveRequests").WithSummary("Moje wnioski urlopowe");

        me.MapPost("/leave-requests", async (MyLeaveRequest request, PortalService portal, CancellationToken cancellationToken) =>
            {
                var result = await portal.CreateMyRequestAsync(request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/me/leave-requests/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .WithName("CreateMyLeaveRequest").WithSummary("Złóż wniosek urlopowy");

        me.MapPost("/leave-requests/{id:guid}/withdraw", async (Guid id, PortalService portal, CancellationToken cancellationToken) =>
                (await portal.WithdrawMyRequestAsync(id, cancellationToken)).ToOk())
            .WithName("WithdrawMyLeaveRequest").WithSummary("Wycofaj swój wniosek, który czeka na decyzję");

        var team = me.MapGroup("/team/leave-requests");

        team.MapGet("/", async (LeaveState? state, int? year, PortalService portal, CancellationToken cancellationToken) =>
                (await portal.ListTeamRequestsAsync(state, year, cancellationToken)).ToOk())
            .WithName("ListTeamLeaveRequests").WithSummary("Wnioski osób, którym jestem przełożonym");

        team.MapPost("/{id:guid}/approve", async (Guid id, LeaveDecisionRequest decision, PortalService portal, CancellationToken cancellationToken) =>
                (await portal.ApproveTeamRequestAsync(id, decision, cancellationToken)).ToOk())
            .WithName("ApproveTeamLeaveRequest").WithSummary("Zatwierdź wniosek osoby z zespołu");

        team.MapPost("/{id:guid}/reject", async (Guid id, LeaveDecisionRequest decision, PortalService portal, CancellationToken cancellationToken) =>
                (await portal.RejectTeamRequestAsync(id, decision, cancellationToken)).ToOk())
            .WithName("RejectTeamLeaveRequest").WithSummary("Odrzuć wniosek osoby z zespołu (z powodem)");

        return api;
    }
}
