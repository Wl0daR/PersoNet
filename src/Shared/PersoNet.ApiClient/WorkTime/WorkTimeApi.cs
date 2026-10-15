using System.Globalization;
using System.Net.Http.Json;
using PersoNet.Contracts.WorkTime;

namespace PersoNet.ApiClient.WorkTime;

internal sealed class WorkTimeApi(HttpClient http) : IWorkTimeApi
{
    public async Task<TimesheetResponse> GetTimesheetAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(Month(employeeId, year, month), UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<TimesheetResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimesheetResponse> FillFromPlanAsync(Guid employeeId, int year, int month, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(new Uri(Month(employeeId, year, month) + "/fill", UriKind.Relative), null, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<TimesheetResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimesheetResponse> SaveDayAsync(Guid employeeId, DateOnly day, SaveWorkDayRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(Day(employeeId, day), request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<TimesheetResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TimesheetResponse> ClearDayAsync(Guid employeeId, DateOnly day, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(new Uri(Day(employeeId, day), UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<TimesheetResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetClockIdAsync(Guid employeeId, SaveClockIdRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"api/v1/employees/{employeeId}/clock-id", request, cancellationToken).ConfigureAwait(false);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImportClockEventsResponse> ImportEventsAsync(ImportClockEventsRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/v1/clock-events/import", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<ImportClockEventsResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImportWorkDaysResponse> ImportDaysAsync(ImportWorkDaysRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/v1/work-days/import", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<ImportWorkDaysResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private static string Month(Guid employeeId, int year, int month) =>
        string.Create(CultureInfo.InvariantCulture, $"api/v1/employees/{employeeId}/work-time/{year}/{month}");

    private static string Day(Guid employeeId, DateOnly date) =>
        string.Create(CultureInfo.InvariantCulture, $"api/v1/employees/{employeeId}/work-time/days/{date:yyyy-MM-dd}");
}
