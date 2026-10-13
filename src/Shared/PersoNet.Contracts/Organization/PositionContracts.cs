namespace PersoNet.Contracts.Organization;

public sealed record PositionResponse(Guid Id, string Name, bool IsActive);

public sealed record CreatePositionRequest(string Name);

public sealed record UpdatePositionRequest(string Name);
