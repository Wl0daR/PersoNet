using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client;
using PersoNet.ApiClient.Authentication;
using PersoNet.Contracts;
using PersoNet.Desktop.Presentation.Authentication;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PersoNet.Desktop.Authentication;

internal static class DesktopAuthentication
{
    /// <summary>
    /// OpenIddict client for PersoNet.Identity. The address comes from "Identity:Authority"
    /// (default: the local development login server).
    /// </summary>
    public static IServiceCollection AddDesktopAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var authority = new Uri(configuration["Identity:Authority"] ?? "https://localhost:7057/");

        services
            .AddOpenIddict()
            .AddClient(options =>
            {
                options.AllowAuthorizationCodeFlow().AllowRefreshTokenFlow();

                // Keys only protect the state of a sign-in in progress, so short-lived in-memory keys are enough,
                // and that state needs no database: tokens live in memory in OpenIddictSignInService.
                options.AddEphemeralEncryptionKey().AddEphemeralSigningKey();
                options.DisableTokenStorage();

                options.UseSystemIntegration()
                    .SetAllowedEmbeddedWebServerPorts([.. PersoNetAuth.DesktopCallbackPorts]);

                options.UseSystemNetHttp().SetProductInformation(typeof(DesktopAuthentication).Assembly);

                options.AddRegistration(new OpenIddictClientRegistration
                {
                    Issuer = authority,
                    ClientId = PersoNetAuth.DesktopClientId,
                    RedirectUri = new Uri(PersoNetAuth.DesktopCallbackPath, UriKind.Relative),
                    Scopes = { Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.OfflineAccess, PersoNetAuth.ApiScope },
                });
            });

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<OpenIddictSignInService>();
        services.AddSingleton<ISignInService>(provider => provider.GetRequiredService<OpenIddictSignInService>());
        services.AddSingleton<IAccessTokenProvider>(provider => provider.GetRequiredService<OpenIddictSignInService>());

        return services;
    }
}
