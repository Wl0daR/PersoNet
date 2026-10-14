using System.Globalization;
using Microsoft.AspNetCore.Authentication;

namespace PersoNet.Web.Authentication;

/// <summary>The tokens of one signed-in session: the API access token, its expiry and the refresh token.</summary>
internal sealed record TokenSet(string AccessToken, string? RefreshToken, DateTimeOffset ExpiresAt)
{
    private const string AccessTokenName = "access_token";
    private const string RefreshTokenName = "refresh_token";
    private const string ExpiresAtName = "expires_at";

    /// <summary>The tokens saved in the sign-in cookie, or null when it has none.</summary>
    public static TokenSet? From(AuthenticationProperties? properties)
    {
        var access = properties?.GetTokenValue(AccessTokenName);
        if (properties is null || string.IsNullOrEmpty(access))
        {
            return null;
        }

        var expiresAt = DateTimeOffset.TryParse(properties.GetTokenValue(ExpiresAtName), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at)
            ? at
            : DateTimeOffset.MinValue;

        return new TokenSet(access, properties.GetTokenValue(RefreshTokenName), expiresAt);
    }

    /// <summary>Writes these tokens into the sign-in cookie's properties.</summary>
    public void WriteTo(AuthenticationProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        // UpdateTokenValue only changes tokens that are already there, so the whole list is stored again.
        var tokens = properties.GetTokens().ToDictionary(token => token.Name, token => token.Value, StringComparer.Ordinal);
        tokens[AccessTokenName] = AccessToken;
        tokens[ExpiresAtName] = ExpiresAt.ToString("o", CultureInfo.InvariantCulture);
        if (RefreshToken is not null)
        {
            tokens[RefreshTokenName] = RefreshToken;
        }

        properties.StoreTokens(tokens.Select(token => new AuthenticationToken { Name = token.Key, Value = token.Value }));
    }
}
