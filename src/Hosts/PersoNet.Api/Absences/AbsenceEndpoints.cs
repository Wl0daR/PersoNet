using PersoNet.Api.Common;
using PersoNet.Application.Absences;
using PersoNet.Application.Abstractions;
using PersoNet.Contracts.Absences;

namespace PersoNet.Api.Absences;

/// <summary>
/// The register of absences that are not leave (sickness, business trips, remote work). Reading needs
/// <see cref="Permissions.LeaveRead"/>, recording needs <see cref="Permissions.LeaveManage"/>, as for leave.
/// </summary>
internal static class AbsenceEndpoints
{
    public static RouteGroupBuilder MapAbsenceEndpoints(this RouteGroupBuilder api)
    {
        var employee = api.MapGroup("/employees/{employeeId:guid}/absences").WithTags("Nieobecności");

        employee.MapGet("/", async (Guid employeeId, int? year, AbsenceService service, CancellationToken cancellationToken) =>
                (await service.ListAsync(employeeId, year, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.LeaveRead)
            .WithName("ListAbsences").WithSummary("Nieobecności pracownika (w roku albo wszystkie), od najnowszej");

        employee.MapPost("/", async (Guid employeeId, SaveAbsenceRequest request, AbsenceService service, CancellationToken cancellationToken) =>
            {
                var result = await service.CreateAsync(employeeId, request, cancellationToken);
                return result.IsSuccess
                    ? TypedResults.Created($"/api/v1/absences/{result.Value.Id}", result.Value)
                    : result.Error!.ToProblem();
            })
            .RequireAuthorization(Permissions.LeaveManage)
            .WithName("CreateAbsence").WithSummary("Wpisz nieobecność pracownika");

        var absences = api.MapGroup("/absences").WithTags("Nieobecności");

        absences.MapPut("/{id:guid}", async (Guid id, SaveAbsenceRequest request, AbsenceService service, CancellationToken cancellationToken) =>
                (await service.UpdateAsync(id, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.LeaveManage)
            .WithName("UpdateAbsence").WithSummary("Popraw nieobecność");

        absences.MapDelete("/{id:guid}", async (Guid id, AbsenceService service, CancellationToken cancellationToken) =>
                (await service.DeleteAsync(id, cancellationToken)).ToNoContent())
            .RequireAuthorization(Permissions.LeaveManage)
            .WithName("DeleteAbsence").WithSummary("Usuń nieobecność wpisaną przez pomyłkę");

        return api;
    }
}
