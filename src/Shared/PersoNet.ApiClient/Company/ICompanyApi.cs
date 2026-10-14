using PersoNet.Contracts.Company;

namespace PersoNet.ApiClient.Company;

/// <summary>The company's data, leave and working time settings. Failures throw <see cref="ApiException"/>.</summary>
public interface ICompanyApi
{
    Task<CompanyResponse> GetAsync(CancellationToken cancellationToken);

    Task<CompanyResponse> SaveDetailsAsync(SaveCompanyDetailsRequest request, CancellationToken cancellationToken);

    Task<CompanyResponse> SaveLeaveSettingsAsync(SaveLeaveSettingsRequest request, CancellationToken cancellationToken);

    Task<CompanyResponse> SaveWorkTimeSettingsAsync(SaveWorkTimeSettingsRequest request, CancellationToken cancellationToken);
}
