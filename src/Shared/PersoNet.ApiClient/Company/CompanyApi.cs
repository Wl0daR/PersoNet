using System.Net.Http.Json;
using PersoNet.Contracts.Company;

namespace PersoNet.ApiClient.Company;

internal sealed class CompanyApi(HttpClient http) : ICompanyApi
{
    private const string Company = "api/v1/company";

    public async Task<CompanyResponse> GetAsync(CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri(Company, UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<CompanyResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CompanyResponse> SaveDetailsAsync(SaveCompanyDetailsRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(Company + "/details", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<CompanyResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CompanyResponse> SaveLeaveSettingsAsync(SaveLeaveSettingsRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(Company + "/leave", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<CompanyResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<CompanyResponse> SaveWorkTimeSettingsAsync(SaveWorkTimeSettingsRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync(Company + "/work-time", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<CompanyResponse>(response, cancellationToken).ConfigureAwait(false);
    }
}
