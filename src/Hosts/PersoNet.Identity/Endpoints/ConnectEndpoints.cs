using System.Security.Claims;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using PersoNet.Contracts;
using PersoNet.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PersoNet.Identity.Endpoints;

/// <summary>
/// The OpenID Connect endpoints OpenIddict passes through to the app: authorization, end session and user info.
/// Token issuance for codes and refresh tokens is handled by OpenIddict itself.
/// </summary>
internal static class ConnectEndpoints
{
    public static IEndpointRouteBuilder MapConnectEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapMethods("/connect/authorize", [HttpMethods.Get, HttpMethods.Post], AuthorizeAsync);
        app.MapMethods("/connect/logout", [HttpMethods.Get, HttpMethods.Post], LogoutAsync);
        app.MapMethods("/connect/userinfo", [HttpMethods.Get, HttpMethods.Post], UserInfoAsync)
            .RequireAuthorization(policy => policy
                .AddAuthenticationSchemes(OpenIddict.Validation.AspNetCore.OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser());

        return app;
    }

    private static async Task<IResult> AuthorizeAsync(HttpContext context, UserManager<ApplicationUser> users)
    {
        var request = context.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("The OpenID Connect request cannot be retrieved.");

        var cookie = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
        var user = cookie.Succeeded ? await users.GetUserAsync(cookie.Principal) : null;

        // Not signed in (or the account is gone): show the login page, then come back here with the same request.
        if (user is null || request.HasPromptValue(PromptValues.Login))
        {
            var parameters = context.Request.HasFormContentType
                ? context.Request.Form.Where(parameter => parameter.Key != Parameters.Prompt).ToList()
                : context.Request.Query.Where(parameter => parameter.Key != Parameters.Prompt).ToList();

            return Results.Challenge(
                new AuthenticationProperties { RedirectUri = context.Request.PathBase + context.Request.Path + QueryString.Create(parameters) },
                [IdentityConstants.ApplicationScheme]);
        }

        var identity = new ClaimsIdentity(
            authenticationType: TokenValidationParameters.DefaultAuthenticationType,
            nameType: Claims.Name,
            roleType: Claims.Role);

        identity
            .SetClaim(Claims.Subject, user.Id.ToString())
            .SetClaim(Claims.Email, user.Email)
            .SetClaim(Claims.Name, user.DisplayName)
            .SetClaim(PersoNetAuth.TenantIdClaim, user.TenantId.ToString())
            .SetClaims(Claims.Role, [.. await users.GetRolesAsync(user)]);

        identity.SetScopes(request.GetScopes());
        if (identity.HasScope(PersoNetAuth.ApiScope))
        {
            identity.SetResources(PersoNetAuth.ApiResource);
        }

        identity.SetDestinations(claim => GetDestinations(claim, identity));

        return Results.SignIn(new ClaimsPrincipal(identity), authenticationScheme: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    private static async Task<IResult> LogoutAsync(SignInManager<ApplicationUser> signIn)
    {
        await signIn.SignOutAsync();

        // OpenIddict validates post_logout_redirect_uri against the client registration and redirects there.
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
    }

    private static async Task<IResult> UserInfoAsync(ClaimsPrincipal principal, UserManager<ApplicationUser> users)
    {
        var user = await users.FindByIdAsync(principal.GetClaim(Claims.Subject) ?? string.Empty);
        if (user is null)
        {
            return Results.Challenge(authenticationSchemes: [OpenIddictServerAspNetCoreDefaults.AuthenticationScheme]);
        }

        var claims = new Dictionary<string, object>(StringComparer.Ordinal)
        {
            [Claims.Subject] = user.Id.ToString(),
            [PersoNetAuth.TenantIdClaim] = user.TenantId.ToString(),
        };

        if (principal.HasScope(Scopes.Email))
        {
            claims[Claims.Email] = user.Email ?? string.Empty;
        }

        if (principal.HasScope(Scopes.Profile))
        {
            claims[Claims.Name] = user.DisplayName;
        }

        return Results.Ok(claims);
    }

    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsIdentity identity) => claim.Type switch
    {
        Claims.Subject or PersoNetAuth.TenantIdClaim or Claims.Role => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Name when identity.HasScope(Scopes.Profile) => [Destinations.AccessToken, Destinations.IdentityToken],
        Claims.Email when identity.HasScope(Scopes.Email) => [Destinations.AccessToken, Destinations.IdentityToken],
        _ => [Destinations.AccessToken],
    };
}
