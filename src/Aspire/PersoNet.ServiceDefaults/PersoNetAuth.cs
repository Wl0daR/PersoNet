namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Names shared by the identity server, the API and the clients.
/// </summary>
public static class PersoNetAuth
{
    /// <summary>OAuth scope that grants access to PersoNet.Api.</summary>
    public const string ApiScope = "personet_api";

    /// <summary>Audience (resource) of access tokens for PersoNet.Api.</summary>
    public const string ApiResource = "personet-api";

    /// <summary>Claim with the user's company id.</summary>
    public const string TenantIdClaim = "tenant_id";

    public const string WebClientId = "personet-web";

    /// <summary>
    /// Demo data created on startup in Development only, so a fresh local setup can sign in.
    /// </summary>
    public static class DevelopmentSeed
    {
        public static readonly Guid TenantId = new("0199e0a0-0000-7000-8000-000000000001");

        public const string TenantName = "Firma demonstracyjna";

        public const string TenantSubdomain = "demo";

        public const string UserEmail = "admin@demo.personet.local";

        public const string UserPassword = "Demo!Haslo123";
    }
}
