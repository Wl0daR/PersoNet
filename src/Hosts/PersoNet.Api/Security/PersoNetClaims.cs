using PersoNet.Contracts;
namespace PersoNet.Api.Security;

/// <summary>
/// Claim types in PersoNet access tokens. Inbound claim mapping is off, so they keep their JWT names.
/// </summary>
internal static class PersoNetClaims
{
    public const string Subject = "sub";

    public const string Name = "name";

    public const string Email = "email";

    public const string Role = "role";

    public const string TenantId = PersoNetAuth.TenantIdClaim;
}
