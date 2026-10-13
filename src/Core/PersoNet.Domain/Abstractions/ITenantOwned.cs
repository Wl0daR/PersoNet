namespace PersoNet.Domain.Abstractions;

/// <summary>
/// Marks data that belongs to a single tenant (company). Every query on such data is filtered by tenant.
/// </summary>
public interface ITenantOwned
{
    Guid TenantId { get; }
}
