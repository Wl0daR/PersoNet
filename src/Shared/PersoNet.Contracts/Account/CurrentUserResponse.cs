namespace PersoNet.Contracts.Account;

/// <summary>
/// The signed-in user, as seen by the API.
/// </summary>
public sealed record CurrentUserResponse(Guid UserId, Guid TenantId, string? Name, string? Email, IReadOnlyList<string> Permissions);
