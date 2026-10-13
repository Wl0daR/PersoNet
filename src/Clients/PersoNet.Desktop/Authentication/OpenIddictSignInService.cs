using OpenIddict.Client;
using PersoNet.ApiClient.Authentication;
using PersoNet.Desktop.Presentation.Authentication;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PersoNet.Desktop.Authentication;

/// <summary>
/// Signs in through the system browser (authorization code + PKCE). The login page calls back to a local port the app
/// listens on. Tokens are kept in memory only, so every start of the app asks for a fresh sign-in.
/// </summary>
internal sealed class OpenIddictSignInService(OpenIddictClientService client, TimeProvider time) : ISignInService, IAccessTokenProvider, IDisposable
{
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset? _expiresAt;
    private string? _refreshToken;

    public void Dispose() => _lock.Dispose();

    public async Task<SignedInUser> SignInAsync(CancellationToken cancellationToken)
    {
        var challenge = await client.ChallengeInteractivelyAsync(new() { CancellationToken = cancellationToken }).ConfigureAwait(false);

        var result = await client.AuthenticateInteractivelyAsync(new()
        {
            CancellationToken = cancellationToken,
            Nonce = challenge.Nonce,
        }).ConfigureAwait(false);

        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _accessToken = result.BackchannelAccessToken;
            _expiresAt = result.BackchannelAccessTokenExpirationDate;
            _refreshToken = result.RefreshToken;
        }
        finally
        {
            _lock.Release();
        }

        var principal = result.Principal;
        return new SignedInUser(
            principal.FindFirst(Claims.Name)?.Value ?? principal.FindFirst(Claims.Email)?.Value ?? "Użytkownik",
            principal.FindFirst(Claims.Email)?.Value);
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _accessToken = null;
            _expiresAt = null;
            _refreshToken = null;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_accessToken is null || _expiresAt is not { } expiresAt || expiresAt - RefreshMargin > time.GetUtcNow())
            {
                return _accessToken;
            }

            if (_refreshToken is null)
            {
                return null;
            }

            var refreshed = await client.AuthenticateWithRefreshTokenAsync(new()
            {
                CancellationToken = cancellationToken,
                RefreshToken = _refreshToken,
            }).ConfigureAwait(false);

            _accessToken = refreshed.AccessToken;
            _expiresAt = refreshed.AccessTokenExpirationDate;
            _refreshToken = refreshed.RefreshToken ?? _refreshToken;

            return _accessToken;
        }
        finally
        {
            _lock.Release();
        }
    }
}
