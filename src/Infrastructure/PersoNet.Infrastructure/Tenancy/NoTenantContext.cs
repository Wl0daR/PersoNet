using PersoNet.Application.Abstractions;

namespace PersoNet.Infrastructure.Tenancy;

/// <summary>
/// Default for hosts without a signed-in user (Worker, tools): no tenant, so tenant-owned data stays hidden.
/// </summary>
public sealed class NoTenantContext : ITenantContext
{
    public Guid? TenantId => null;
}
