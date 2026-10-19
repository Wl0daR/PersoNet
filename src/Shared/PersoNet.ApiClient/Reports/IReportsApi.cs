using PersoNet.Contracts.Reports;

namespace PersoNet.ApiClient.Reports;

/// <summary>Company-wide HR reports. Failures throw <see cref="ApiException"/>.</summary>
public interface IReportsApi
{
    Task<HeadcountReport> GetHeadcountAsync(DateOnly day, CancellationToken cancellationToken);

    Task<LeaveUsageReport> GetLeaveUsageAsync(int year, CancellationToken cancellationToken);

    Task<ContractsEndingReport> GetContractsEndingAsync(DateOnly from, int days, CancellationToken cancellationToken);

    Task<SafetyRenewalReport> GetSafetyRenewalsAsync(int days, CancellationToken cancellationToken);
}
