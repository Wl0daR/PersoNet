using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;
using PersoNet.ApiClient.Authentication;

namespace PersoNet.Web.Authentication;

/// <summary>
/// The access token for API calls made by the web app: the signed-in user's session tokens from
/// <see cref="SessionTokenStore"/>. The user comes from the Blazor circuit in interactive pages and from the HTTP
/// request in statically rendered ones.
/// </summary>
internal sealed class WebAccessTokenProvider(
    CircuitServicesAccessor circuit,
    IHttpContextAccessor httpContext,
    SessionTokenStore store) : IAccessTokenProvider
{
    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        var user = await CurrentUserAsync();
        var session = user?.FindFirst(WebSession.ClaimType)?.Value;
        if (session is null)
        {
            return null;
        }

        // Every request through the sign-in cookie has already seeded the store, so there is nothing to seed from here.
        var tokens = await store.GetFreshAsync(session, fromCookie: null, cancellationToken);
        return tokens?.AccessToken;
    }

    private async Task<ClaimsPrincipal?> CurrentUserAsync()
    {
        if (circuit.Services?.GetService<AuthenticationStateProvider>() is { } state)
        {
            return (await state.GetAuthenticationStateAsync()).User;
        }

        return httpContext.HttpContext?.User;
    }
}
