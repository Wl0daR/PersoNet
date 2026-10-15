using System.Globalization;
using System.Net.Http.Json;
using PersoNet.Contracts.Absences;

namespace PersoNet.ApiClient.Absences;

internal sealed class AbsencesApi(HttpClient http) : IAbsencesApi
{
    private const string Absences = "api/v1/absences";

    public async Task<IReadOnlyList<AbsenceResponse>> ListAsync(Guid employeeId, int? year, CancellationToken cancellationToken)
    {
        var query = OfEmployee(employeeId) + (year is { } y ? "?year=" + y.ToString(CultureInfo.InvariantCulture) : string.Empty);
        using var response = await http.GetAsync(new Uri(query, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<AbsenceResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AbsenceResponse> CreateAsync(Guid employeeId, SaveAbsenceRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(OfEmployee(employeeId), request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<AbsenceResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<AbsenceResponse> UpdateAsync(Guid id, SaveAbsenceRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"{Absences}/{id}", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<AbsenceResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(new Uri($"{Absences}/{id}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImportLeaveHistoryResponse> ImportHistoryAsync(ImportLeaveHistoryRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/v1/leave/import", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<ImportLeaveHistoryResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    private static string OfEmployee(Guid employeeId) => $"api/v1/employees/{employeeId}/absences";
}
