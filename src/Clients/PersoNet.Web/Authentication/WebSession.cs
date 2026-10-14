using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace PersoNet.Web.Authentication;

/// <summary>
/// One sign-in to the web app. It gets its own id, kept as a claim in the cookie, under which
/// <see cref="SessionTokenStore"/> keeps the session's tokens.
/// </summary>
internal static class WebSession
{
    public const string ClaimType = "personet_web_session";

    /// <summary>Gives a fresh sign-in its session id.</summary>
    public static void Start(ClaimsPrincipal principal) =>
        (principal.Identity as ClaimsIdentity)?.AddClaim(new Claim(ClaimType, Guid.NewGuid().ToString("N")));

    /// <summary>
    /// Runs on every request with the sign-in cookie: keeps the session's access token fresh and writes the newest tokens
    /// back into the cookie. A session whose tokens cannot be refreshed is signed out, so the next page asks to sign in.
    /// </summary>
    public static async Task ValidateAsync(CookieValidatePrincipalContext context)
    {
        var session = context.Principal?.FindFirst(ClaimType)?.Value;
        if (session is null)
        {
            // A cookie from before sessions had ids.
            await RejectAsync(context);
            return;
        }

        var store = context.HttpContext.RequestServices.GetRequiredService<SessionTokenStore>();
        var fromCookie = TokenSet.From(context.Properties);
        var tokens = await store.GetFreshAsync(session, fromCookie, context.HttpContext.RequestAborted);
        if (tokens is null)
        {
            await RejectAsync(context);
            return;
        }

        if (tokens != fromCookie)
        {
            tokens.WriteTo(context.Properties);
            context.ShouldRenew = true;
        }
    }

    /// <summary>Forgets the tokens of the signed-in session.</summary>
    public static void End(HttpContext context)
    {
        if (context.User.FindFirst(ClaimType)?.Value is { } session)
        {
            context.RequestServices.GetRequiredService<SessionTokenStore>().Remove(session);
        }
    }

    private static async Task RejectAsync(CookieValidatePrincipalContext context)
    {
        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
