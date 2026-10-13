namespace PersoNet.Application.Abstractions;

/// <summary>
/// The company whose data the current operation may see. Resolved from the access token, never from the request URL.
/// </summary>
public interface ITenantContext
{
    /// <summary>
    /// The current tenant, or <see langword="null"/> when none is known; tenant-owned data is then hidden.
    /// </summary>
    Guid? TenantId { get; }
}
