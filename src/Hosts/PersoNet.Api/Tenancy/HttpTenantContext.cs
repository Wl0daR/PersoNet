using PersoNet.Application.Abstractions;

namespace PersoNet.Api.Tenancy;

/// <summary>
/// Reads the caller's company from the <c>tenant_id</c> claim of the access token issued by PersoNet.Identity.
/// </summary>
internal sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    public const string TenantIdClaim = "tenant_id";

    public Guid? TenantId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(TenantIdClaim)?.Value, out var tenantId)
            ? tenantId
            : null;
}
