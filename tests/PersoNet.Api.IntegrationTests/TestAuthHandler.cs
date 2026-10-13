using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PersoNet.Api.IntegrationTests;

/// <summary>
/// Signs every request in as a fixed user, standing in for an access token from PersoNet.Identity.
/// </summary>
internal sealed class TestAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "Test";

    public static readonly Guid UserId = Guid.CreateVersion7();

    public static readonly Guid TenantId = Guid.CreateVersion7();

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        Claim[] claims =
        [
            new("sub", UserId.ToString()),
            new("name", "Anna Kowalska"),
            new("email", "anna@acme.test"),
            new(PersoNetAuth.TenantIdClaim, TenantId.ToString()),
        ];

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "name", "role"));

        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
    }
}
