using PersoNet.Contracts.Organization;

namespace PersoNet.ApiClient.Organization;

/// <summary>
/// Company structure endpoints of PersoNet.Api. Failures throw <see cref="ApiException"/>.
/// </summary>
public interface IOrganizationApi
{
    Task<IReadOnlyList<DepartmentResponse>> GetDepartmentsAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request, CancellationToken cancellationToken);

    Task<DepartmentResponse> UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken);

    Task SetDepartmentActiveAsync(Guid id, bool active, CancellationToken cancellationToken);

    Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<PositionResponse> CreatePositionAsync(CreatePositionRequest request, CancellationToken cancellationToken);

    Task<PositionResponse> UpdatePositionAsync(Guid id, UpdatePositionRequest request, CancellationToken cancellationToken);

    Task SetPositionActiveAsync(Guid id, bool active, CancellationToken cancellationToken);
}
