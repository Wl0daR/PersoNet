using System.Globalization;
using PersoNet.Contracts.Reports;

namespace PersoNet.ApiClient.Reports;

internal sealed class ReportsApi(HttpClient http) : IReportsApi
{
    private const string Reports = "api/v1/reports";

    public Task<HeadcountReport> GetHeadcountAsync(DateOnly day, CancellationToken cancellationToken) =>
        GetAsync<HeadcountReport>($"{Reports}/headcount?date={Format(day)}", cancellationToken);

    public Task<LeaveUsageReport> GetLeaveUsageAsync(int year, CancellationToken cancellationToken) =>
        GetAsync<LeaveUsageReport>(string.Create(CultureInfo.InvariantCulture, $"{Reports}/leave-usage?year={year}"), cancellationToken);

    public Task<ContractsEndingReport> GetContractsEndingAsync(DateOnly from, int days, CancellationToken cancellationToken) =>
        GetAsync<ContractsEndingReport>(string.Create(CultureInfo.InvariantCulture, $"{Reports}/contracts-ending?from={Format(from)}&days={days}"), cancellationToken);

    private static string Format(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private async Task<T> GetAsync<T>(string uri, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(uri, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<T>(response, cancellationToken).ConfigureAwait(false);
    }
}
