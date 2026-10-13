using System.Net;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Organization;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>
/// In-memory stand-in for the API with the same rules the server enforces that the screens rely on.
/// </summary>
internal sealed class FakeOrganizationApi : IOrganizationApi
{
    public List<DepartmentResponse> Departments { get; } = [];

    public List<PositionResponse> Positions { get; } = [];

    public Task<IReadOnlyList<DepartmentResponse>> GetDepartmentsAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<DepartmentResponse>>(Departments.Where(d => includeInactive || d.IsActive).ToList());

    public Task<DepartmentResponse> CreateDepartmentAsync(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        if (Departments.Any(d => string.Equals(d.Code, request.Code, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiException(HttpStatusCode.Conflict, "Dział o tym kodzie już istnieje.", "department.code_taken");
        }

        var department = new DepartmentResponse(Guid.CreateVersion7(), request.Name, request.Code.ToUpperInvariant(), request.ParentId, IsActive: true);
        Departments.Add(department);
        return Task.FromResult(department);
    }

    public Task<DepartmentResponse> UpdateDepartmentAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var index = Departments.FindIndex(d => d.Id == id);
        var updated = Departments[index] with { Name = request.Name, Code = request.Code.ToUpperInvariant(), ParentId = request.ParentId };
        Departments[index] = updated;
        return Task.FromResult(updated);
    }

    public Task SetDepartmentActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        if (!active && Departments.Any(d => d.ParentId == id && d.IsActive))
        {
            throw new ApiException(HttpStatusCode.Conflict, "Najpierw dezaktywuj poddziały tego działu.", "department.has_active_children");
        }

        var index = Departments.FindIndex(d => d.Id == id);
        Departments[index] = Departments[index] with { IsActive = active };
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PositionResponse>> GetPositionsAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<PositionResponse>>(Positions.Where(p => includeInactive || p.IsActive).ToList());

    public Task<PositionResponse> CreatePositionAsync(CreatePositionRequest request, CancellationToken cancellationToken)
    {
        if (Positions.Any(p => string.Equals(p.Name, request.Name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiException(HttpStatusCode.Conflict, "Stanowisko o tej nazwie już istnieje.", "position.name_taken");
        }

        var position = new PositionResponse(Guid.CreateVersion7(), request.Name, IsActive: true);
        Positions.Add(position);
        return Task.FromResult(position);
    }

    public Task<PositionResponse> UpdatePositionAsync(Guid id, UpdatePositionRequest request, CancellationToken cancellationToken)
    {
        var index = Positions.FindIndex(p => p.Id == id);
        Positions[index] = Positions[index] with { Name = request.Name };
        return Task.FromResult(Positions[index]);
    }

    public Task SetPositionActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        var index = Positions.FindIndex(p => p.Id == id);
        Positions[index] = Positions[index] with { IsActive = active };
        return Task.CompletedTask;
    }

    public DepartmentResponse Add(string name, string code, Guid? parentId = null, bool active = true)
    {
        var department = new DepartmentResponse(Guid.CreateVersion7(), name, code, parentId, active);
        Departments.Add(department);
        return department;
    }
}
