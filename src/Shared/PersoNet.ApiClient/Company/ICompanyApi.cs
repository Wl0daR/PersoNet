using PersoNet.Contracts.Company;

namespace PersoNet.ApiClient.Company;

/// <summary>The company's data and leave settings. Failures throw <see cref="ApiException"/>.</summary>
public interface ICompanyApi
{
    Task<CompanyResponse> GetAsync(CancellationToken cancellationToken);

    Task<CompanyResponse> SaveDetailsAsync(SaveCompanyDetailsRequest request, CancellationToken cancellationToken);

    Task<CompanyResponse> SaveLeaveSettingsAsync(SaveLeaveSettingsRequest request, CancellationToken cancellationToken);
}
