using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace PersoNet.Web.Authentication;

/// <summary>
/// Refreshes tokens at the token endpoint of the OpenID Connect sign-in, with the same client id, secret and back channel.
/// </summary>
internal sealed partial class OidcTokenRefresher(
    IOptionsMonitor<OpenIdConnectOptions> oidcOptions,
    TimeProvider time,
    ILogger<OidcTokenRefresher> logger) : ITokenRefresher
{
    public async Task<TokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var options = oidcOptions.Get(OpenIdConnectDefaults.AuthenticationScheme);
        var configuration = await options.ConfigurationManager!.GetConfigurationAsync(cancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConnectGrantTypes.RefreshToken,
                ["refresh_token"] = refreshToken,
                ["client_id"] = options.ClientId!,
                ["client_secret"] = options.ClientSecret!,
            }),
        };

        using var response = await options.Backchannel.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            LogRefreshRefused(logger, (int)response.StatusCode);
            return null;
        }

        var message = new OpenIdConnectMessage(body);
        var expiresIn = int.TryParse(message.ExpiresIn, out var seconds) ? seconds : 0;

        return new TokenSet(message.AccessToken, message.RefreshToken ?? refreshToken, time.GetUtcNow().AddSeconds(expiresIn));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "The identity server refused to refresh the session's tokens ({StatusCode}); the user must sign in again.")]
    private static partial void LogRefreshRefused(ILogger logger, int statusCode);
}
