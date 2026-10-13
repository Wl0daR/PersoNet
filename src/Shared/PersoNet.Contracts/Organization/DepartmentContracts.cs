namespace PersoNet.Contracts.Organization;

public sealed record DepartmentResponse(Guid Id, string Name, string Code, Guid? ParentId, bool IsActive);

public sealed record CreateDepartmentRequest(string Name, string Code, Guid? ParentId);

public sealed record UpdateDepartmentRequest(string Name, string Code, Guid? ParentId);
