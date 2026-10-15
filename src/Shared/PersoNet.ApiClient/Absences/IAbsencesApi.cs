using PersoNet.Contracts.Absences;

namespace PersoNet.ApiClient.Absences;

/// <summary>Absences that are not leave (sickness, business trips, remote work) in PersoNet.Api. Failures throw <see cref="ApiException"/>.</summary>
public interface IAbsencesApi
{
    /// <summary>Absences of the employee touching the year, or all of them when <paramref name="year"/> is null.</summary>
    Task<IReadOnlyList<AbsenceResponse>> ListAsync(Guid employeeId, int? year, CancellationToken cancellationToken);

    Task<AbsenceResponse> CreateAsync(Guid employeeId, SaveAbsenceRequest request, CancellationToken cancellationToken);

    Task<AbsenceResponse> UpdateAsync(Guid id, SaveAbsenceRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Leave, absences and entitlements of the earlier system, for employees already imported.</summary>
    Task<ImportLeaveHistoryResponse> ImportHistoryAsync(ImportLeaveHistoryRequest request, CancellationToken cancellationToken);
}
