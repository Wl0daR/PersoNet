using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace PersoNet.Web.Authentication;

/// <summary>
/// Sign-in through PersoNet.Identity (OpenID Connect, authorization code + PKCE). The session lives in a cookie.
/// </summary>
internal static class AuthenticationSetup
{
    public static WebApplicationBuilder AddPersoNetAuthentication(this WebApplicationBuilder builder)
    {
        // Locally the address comes from Aspire service discovery; on IIS it is set in "Identity:Authority".
        var authority = builder.Configuration["Identity:Authority"]
            ?? builder.Configuration["services:identity:https:0"]
            ?? throw new InvalidOperationException("Configure Identity:Authority (the PersoNet.Identity address).");

        var clientSecret = builder.Configuration["Identity:ClientSecret"]
            ?? throw new InvalidOperationException("Configure Identity:ClientSecret (the personet-web client secret).");

        builder.Services
            .AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.Cookie.Name = "personet.web";
                options.ExpireTimeSpan = TimeSpan.FromHours(8);
                options.SlidingExpiration = true;
            })
            .AddOpenIdConnect(options =>
            {
                options.Authority = authority;
                options.ClientId = PersoNetAuth.WebClientId;
                options.ClientSecret = clientSecret;

                options.ResponseType = OpenIdConnectResponseType.Code;
                options.UsePkce = true;
                options.SaveTokens = true;
                options.GetClaimsFromUserInfoEndpoint = true;

                // Keep the claim names from the tokens ("sub", "name", "tenant_id").
                options.MapInboundClaims = false;
                options.TokenValidationParameters.NameClaimType = "name";
                options.TokenValidationParameters.RoleClaimType = "role";
                options.ClaimActions.MapUniqueJsonKey(PersoNetAuth.TenantIdClaim, PersoNetAuth.TenantIdClaim);

                options.Scope.Clear();
                options.Scope.Add(OpenIdConnectScope.OpenId);
                options.Scope.Add(OpenIdConnectScope.OfflineAccess);
                options.Scope.Add("profile");
                options.Scope.Add("email");
                options.Scope.Add(PersoNetAuth.ApiScope);
            });

        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();

        return builder;
    }

    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/account");

        account.MapGet("/login", (string? returnUrl) =>
                TypedResults.Challenge(new AuthenticationProperties { RedirectUri = LocalOrRoot(returnUrl) }))
            .AllowAnonymous();

        account.MapPost("/logout", () =>
                TypedResults.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]))
            .RequireAuthorization();

        return app;
    }

    private static string LocalOrRoot(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";
}
