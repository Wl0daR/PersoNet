namespace PersoNet.Application.Abstractions;

/// <summary>
/// The authenticated caller, resolved from the access token by the host. The caller's company comes from <see cref="ITenantContext"/>.
/// </summary>
public interface ICurrentUser
{
    Guid UserId { get; }

    bool HasPermission(string permission);
}
