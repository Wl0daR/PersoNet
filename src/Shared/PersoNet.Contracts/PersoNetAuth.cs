namespace PersoNet.Contracts;

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

    /// <summary>HR desktop app. Signs in through the system browser and receives the callback on a local port.</summary>
    public const string DesktopClientId = "personet-desktop";

    /// <summary>
    /// Local ports the desktop app listens on for the sign-in callback; the first free one is used.
    /// Each is registered as a redirect URI, because the login server matches redirect URIs exactly.
    /// </summary>
    public static IReadOnlyList<int> DesktopCallbackPorts { get; } = [58740, 58741, 58742];

    public const string DesktopCallbackPath = "callback/login/local";

    /// <summary>Interactive API documentation (Scalar), Development only.</summary>
    public const string ApiDocsClientId = "personet-api-docs";

    /// <summary>
    /// Roles carried in the "role" claim. The API maps them to permissions; endpoints never check roles directly.
    /// </summary>
    public static class Roles
    {
        /// <summary>HR department: configures the company and manages employee records.</summary>
        public const string HrAdmin = "hr_admin";

        public static IReadOnlyList<string> All { get; } = [HrAdmin];
    }

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
