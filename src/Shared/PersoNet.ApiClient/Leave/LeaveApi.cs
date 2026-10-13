using System.Globalization;
using System.Net.Http.Json;
using PersoNet.Contracts.Leave;

namespace PersoNet.ApiClient.Leave;

internal sealed class LeaveApi(HttpClient http) : ILeaveApi
{
    private const string Requests = "api/v1/leave-requests";

    public async Task<IReadOnlyList<LeaveRequestResponse>> ListAsync(LeaveState? state, int? year, Guid? employeeId, CancellationToken cancellationToken)
    {
        var parameters = new List<string>();

        if (state is { } wanted)
        {
            parameters.Add("state=" + wanted);
        }

        if (year is { } y)
        {
            parameters.Add("year=" + y.ToString(CultureInfo.InvariantCulture));
        }

        if (employeeId is { } id)
        {
            parameters.Add($"employeeId={id}");
        }

        var query = parameters.Count == 0 ? string.Empty : "?" + string.Join('&', parameters);
        using var response = await http.GetAsync(new Uri(Requests + query, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<LeaveRequestResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LeaveRequestResponse> CreateAsync(CreateLeaveRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(Requests, request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<LeaveRequestResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public Task<LeaveRequestResponse> ApproveAsync(Guid id, string? note, CancellationToken cancellationToken) => DecideAsync(id, "approve", note, cancellationToken);

    public Task<LeaveRequestResponse> RejectAsync(Guid id, string reason, CancellationToken cancellationToken) => DecideAsync(id, "reject", reason, cancellationToken);

    public Task<LeaveRequestResponse> CancelAsync(Guid id, string? note, CancellationToken cancellationToken) => DecideAsync(id, "cancel", note, cancellationToken);

    public async Task<IReadOnlyList<HolidayResponse>> GetHolidaysAsync(int year, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri("api/v1/holidays/" + year.ToString(CultureInfo.InvariantCulture), UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<HolidayResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LeaveBalanceResponse> GetBalanceAsync(Guid employeeId, int year, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(Balance(employeeId, year), UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<LeaveBalanceResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LeaveBalanceResponse> SetEntitlementAsync(Guid employeeId, int year, SaveLeaveEntitlementRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(Balance(employeeId, year), request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<LeaveBalanceResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private static string Balance(Guid employeeId, int year) =>
        $"api/v1/employees/{employeeId}/leave/{year.ToString(CultureInfo.InvariantCulture)}";

    private async Task<LeaveRequestResponse> DecideAsync(Guid id, string action, string? note, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"{Requests}/{id}/{action}", new LeaveDecisionRequest(note), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<LeaveRequestResponse>(response, cancellationToken).ConfigureAwait(false);
    }
}
