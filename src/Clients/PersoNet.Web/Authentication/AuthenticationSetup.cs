using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using PersoNet.ApiClient.Authentication;
using PersoNet.Contracts;

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
                options.Events.OnValidatePrincipal = WebSession.ValidateAsync;
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

                options.Events.OnTokenValidated = context =>
                {
                    WebSession.Start(context.Principal!);
                    return Task.CompletedTask;
                };
            });

        builder.Services.AddAuthorization();
        builder.Services.AddCascadingAuthenticationState();

        // API calls carry the signed-in user's access token, refreshed on the server (see SessionTokenStore).
        builder.Services.AddMemoryCache();
        builder.Services.AddHttpContextAccessor();
        builder.Services.TryAddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<ITokenRefresher, OidcTokenRefresher>();
        builder.Services.AddSingleton<SessionTokenStore>();
        builder.Services.AddSingleton<CircuitServicesAccessor>();
        builder.Services.AddScoped<CircuitHandler, CircuitServicesHandler>();
        builder.Services.AddSingleton<IAccessTokenProvider, WebAccessTokenProvider>();

        return builder;
    }

    public static IEndpointRouteBuilder MapAuthenticationEndpoints(this IEndpointRouteBuilder app)
    {
        var account = app.MapGroup("/account");

        account.MapGet("/login", (string? returnUrl) =>
                TypedResults.Challenge(new AuthenticationProperties { RedirectUri = LocalOrRoot(returnUrl) }))
            .AllowAnonymous();

        account.MapPost("/logout", (HttpContext context) =>
            {
                WebSession.End(context);
                return TypedResults.SignOut(
                    new AuthenticationProperties { RedirectUri = "/" },
                    [CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme]);
            })
            .RequireAuthorization();

        return app;
    }

    private static string LocalOrRoot(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && returnUrl.StartsWith('/') && !returnUrl.StartsWith("//", StringComparison.Ordinal)
            && !returnUrl.StartsWith("/\\", StringComparison.Ordinal)
            ? returnUrl
            : "/";
}
