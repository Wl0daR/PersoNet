namespace PersoNet.Web.Authentication;

/// <summary>Exchanges a refresh token for new tokens at PersoNet.Identity.</summary>
internal interface ITokenRefresher
{
    /// <summary>The new tokens, or null when the identity server refused the refresh token (the user must sign in again).</summary>
    Task<TokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken);
}
