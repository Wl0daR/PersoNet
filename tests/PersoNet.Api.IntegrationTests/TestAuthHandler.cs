using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PersoNet.Contracts;

namespace PersoNet.Api.IntegrationTests;

/// <summary>
/// Signs every request in, standing in for an access token from PersoNet.Identity. By default the user is in HR of the
/// demo company; the <see cref="TenantHeader"/>, <see cref="RolesHeader"/> and <see cref="EmailHeader"/> request headers change that.
/// </summary>
internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    public static readonly Guid UserId = Guid.CreateVersion7();

    public const string TenantHeader = "X-Test-Tenant";

    public const string RolesHeader = "X-Test-Roles";

    public const string EmailHeader = "X-Test-Email";

    /// <summary>The demo company, created by the API on startup in Development.</summary>
    public static readonly Guid TenantId = PersoNetAuth.DevelopmentSeed.TenantId;

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var tenantId = Request.Headers.TryGetValue(TenantHeader, out var tenant) ? tenant.ToString() : TenantId.ToString();
        var roles = Request.Headers.TryGetValue(RolesHeader, out var requested)
            ? requested.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [PersoNetAuth.Roles.HrAdmin];

        List<Claim> claims =
        [
            new("sub", UserId.ToString()),
            new("name", "Anna Kowalska"),
            new("email", Request.Headers.TryGetValue(EmailHeader, out var email) ? email.ToString() : "anna@acme.test"),
            new(PersoNetAuth.TenantIdClaim, tenantId),
        ];
        claims.AddRange(roles.Select(role => new Claim("role", role)));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "name", "role"));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
