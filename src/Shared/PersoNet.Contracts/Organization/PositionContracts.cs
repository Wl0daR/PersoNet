namespace PersoNet.Contracts.Organization;

/// <summary>A position with the departments it belongs to; no departments means it fits any department.</summary>
public sealed record PositionResponse(Guid Id, string Name, bool IsActive, IReadOnlyList<Guid> DepartmentIds);

public sealed record CreatePositionRequest(string Name, IReadOnlyList<Guid>? DepartmentIds = null);

/// <summary>New name and departments of a position; null departments leave them as they are.</summary>
public sealed record UpdatePositionRequest(string Name, IReadOnlyList<Guid>? DepartmentIds = null);
