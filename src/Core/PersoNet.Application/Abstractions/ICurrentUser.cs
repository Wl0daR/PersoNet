namespace PersoNet.Application.Abstractions;

/// <summary>
/// The authenticated caller, resolved from the access token by the host.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    Guid TenantId { get; }

    bool HasPermission(string permission);
}
