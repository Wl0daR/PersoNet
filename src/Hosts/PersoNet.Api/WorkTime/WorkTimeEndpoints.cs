using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.WorkTime;
using PersoNet.Contracts.WorkTime;

namespace PersoNet.Api.WorkTime;

/// <summary>
/// Employees' timesheets and clock events from time clock systems. Reading needs <see cref="Permissions.WorkTimeRead"/>,
/// typing days, importing events and setting time clock identifiers needs <see cref="Permissions.WorkTimeManage"/>.
/// </summary>
internal static class WorkTimeEndpoints
{
    public static RouteGroupBuilder MapWorkTimeEndpoints(this RouteGroupBuilder api)
    {
        var employee = api.MapGroup("/employees/{employeeId:guid}/work-time").WithTags("Czas pracy");

        employee.MapGet("/{year:int}/{month:int}", async (Guid employeeId, int year, int month, WorkTimeService service, CancellationToken cancellationToken) =>
                (await service.GetTimesheetAsync(employeeId, year, month, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.WorkTimeRead)
            .WithName("GetTimesheet").WithSummary("Karta ewidencji czasu pracy pracownika w miesiącu");

        employee.MapPost("/{year:int}/{month:int}/fill", async (Guid employeeId, int year, int month, WorkTimeService service, CancellationToken cancellationToken) =>
                (await service.FillFromPlanAsync(employeeId, year, month, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.WorkTimeManage)
            .WithName("FillTimesheetFromPlan").WithSummary("Wypełnij puste dni robocze miesiąca godzinami z planu (od 8:00, według etatu)");

        employee.MapPut("/days/{date}", async (Guid employeeId, DateOnly date, SaveWorkDayRequest request, WorkTimeService service, CancellationToken cancellationToken) =>
                (await service.SaveDayAsync(employeeId, date, request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.WorkTimeManage)
            .WithName("SaveWorkDay").WithSummary("Wpisz albo popraw dzień: godziny pracy albo nieobecność");

        employee.MapDelete("/days/{date}", async (Guid employeeId, DateOnly date, WorkTimeService service, CancellationToken cancellationToken) =>
                (await service.ClearDayAsync(employeeId, date, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.WorkTimeManage)
            .WithName("ClearWorkDay").WithSummary("Usuń wpis dnia; dzień z odbiciami wraca do godzin z czytnika");

        api.MapPut("/employees/{employeeId:guid}/clock-id", async (Guid employeeId, SaveClockIdRequest request, WorkTimeService service, CancellationToken cancellationToken) =>
                (await service.SetClockIdAsync(employeeId, request, cancellationToken)).ToNoContent())
            .WithTags("Czas pracy")
            .RequireAuthorization(Permissions.WorkTimeManage)
            .WithName("SetClockId").WithSummary("Ustaw identyfikator pracownika w systemie RCP (numer karty lub ID użytkownika)");

        api.MapPost("/work-days/import", async (ImportWorkDaysRequest request, WorkDayImportService service, CancellationToken cancellationToken) =>
                (await service.ImportAsync(request, cancellationToken)).ToOk())
            .WithTags("Czas pracy")
            .RequireAuthorization(Permissions.WorkTimeManage)
            .WithName("ImportWorkDays").WithSummary("Import przepracowanych dni z poprzedniego systemu")
            .WithDescription("Pracownicy są rozpoznawani po numerze ewidencyjnym. Dni już wpisane w PersoNet zostają bez zmian, "
                + "a dni zatwierdzonego urlopu nie są zamieniane na pracę.");

        api.MapPost("/clock-events/import", async (ImportClockEventsRequest request, WorkTimeService service, CancellationToken cancellationToken) =>
                (await service.ImportEventsAsync(request, cancellationToken)).ToOk())
            .WithTags("Czas pracy")
            .RequireAuthorization(Permissions.WorkTimeManage)
            .WithName("ImportClockEvents").WithSummary("Zapisz odbicia wejść i wyjść z systemu RCP (pliku lub integracji); powtórzone odbicia są pomijane");

        return api;
    }
}
