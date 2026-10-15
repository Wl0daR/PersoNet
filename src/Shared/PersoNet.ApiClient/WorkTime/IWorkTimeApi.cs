using PersoNet.Contracts.WorkTime;

namespace PersoNet.ApiClient.WorkTime;

/// <summary>Employees' timesheets and clock events. Failures throw <see cref="ApiException"/>.</summary>
public interface IWorkTimeApi
{
    Task<TimesheetResponse> GetTimesheetAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken);

    Task<TimesheetResponse> FillFromPlanAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken);

    Task<TimesheetResponse> SaveDayAsync(Guid employeeId, DateOnly day, SaveWorkDayRequest request, CancellationToken cancellationToken);

    Task<TimesheetResponse> ClearDayAsync(Guid employeeId, DateOnly day, CancellationToken cancellationToken);

    Task SetClockIdAsync(Guid employeeId, SaveClockIdRequest request, CancellationToken cancellationToken);

    Task<ImportClockEventsResponse> ImportEventsAsync(ImportClockEventsRequest request, CancellationToken cancellationToken);

    /// <summary>Days worked in an earlier system, for employees already imported.</summary>
    Task<ImportWorkDaysResponse> ImportDaysAsync(ImportWorkDaysRequest request, CancellationToken cancellationToken);
}
