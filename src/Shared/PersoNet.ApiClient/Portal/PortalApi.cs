using System.Globalization;
using System.Net.Http.Json;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.SelfService;

namespace PersoNet.ApiClient.Portal;

internal sealed class PortalApi(HttpClient http) : IPortalApi
{
    private const string Me = "api/v1/me";
    private const string MyRequests = Me + "/leave-requests";
    private const string TeamRequests = Me + "/team/leave-requests";

    public async Task<MyProfileResponse> GetProfileAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(Me + "/employee", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<MyProfileResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LeaveBalanceResponse> GetBalanceAsync(int year, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(Me + "/leave/" + year.ToString(CultureInfo.InvariantCulture), UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<LeaveBalanceResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LeaveRequestResponse>> ListMyRequestsAsync(int? year, CancellationToken cancellationToken)
    {
        var query = year is { } y ? "?year=" + y.ToString(CultureInfo.InvariantCulture) : string.Empty;
        using var response = await http.GetAsync(new Uri(MyRequests + query, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<LeaveRequestResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LeaveRequestResponse> CreateMyRequestAsync(MyLeaveRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(MyRequests, request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<LeaveRequestResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<LeaveRequestResponse> WithdrawAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(new Uri($"{MyRequests}/{id}/withdraw", UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<LeaveRequestResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<LeaveRequestResponse>> ListTeamRequestsAsync(LeaveState? state, int? year, CancellationToken cancellationToken)
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

        var query = parameters.Count == 0 ? string.Empty : "?" + string.Join('&', parameters);
        using var response = await http.GetAsync(new Uri(TeamRequests + query, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<LeaveRequestResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public Task<LeaveRequestResponse> ApproveAsync(Guid id, string? note, CancellationToken cancellationToken) => DecideAsync(id, "approve", note, cancellationToken);

    public Task<LeaveRequestResponse> RejectAsync(Guid id, string reason, CancellationToken cancellationToken) => DecideAsync(id, "reject", reason, cancellationToken);

    public async Task<IReadOnlyList<HolidayResponse>> GetHolidaysAsync(int year, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri("api/v1/holidays/" + year.ToString(CultureInfo.InvariantCulture), UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<HolidayResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    private async Task<LeaveRequestResponse> DecideAsync(Guid id, string action, string? note, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"{TeamRequests}/{id}/{action}", new LeaveDecisionRequest(note), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<LeaveRequestResponse>(response, cancellationToken).ConfigureAwait(false);
    }
}
