using Microsoft.Extensions.Caching.Memory;

namespace PersoNet.Web.Authentication;

/// <summary>
/// The newest tokens of each signed-in session, kept on the server. The identity server rotates refresh tokens, and a
/// used refresh token must never be sent again, so cookie requests and Blazor circuits of one session both read and
/// refresh tokens here, one at a time; the cookie only seeds the store and is updated from it on the next request.
/// </summary>
internal sealed class SessionTokenStore(IMemoryCache cache, ITokenRefresher refresher, TimeProvider time)
{
    /// <summary>Tokens closer than this to expiry are refreshed before use, so a call never leaves with a dying token.</summary>
    public static readonly TimeSpan RefreshMargin = TimeSpan.FromMinutes(1);

    /// <summary>Sessions nobody used for this long are forgotten; the cookie seeds them again on the next request.</summary>
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromHours(8);

    private readonly Lock _create = new();

    /// <summary>
    /// A token that is valid for at least <see cref="RefreshMargin"/>, refreshed if needed; null when the session has no
    /// tokens or the refresh was refused. <paramref name="fromCookie"/> seeds a session the store does not know yet.
    /// </summary>
    public async Task<TokenSet?> GetFreshAsync(string session, TokenSet? fromCookie, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(session);

        var entry = Entry(session);
        await entry.Gate.WaitAsync(cancellationToken);
        try
        {
            entry.Tokens ??= fromCookie;
            if (entry.Tokens is not { } tokens)
            {
                return null;
            }

            if (tokens.ExpiresAt - RefreshMargin > time.GetUtcNow())
            {
                return tokens;
            }

            entry.Tokens = tokens.RefreshToken is null ? null : await refresher.RefreshAsync(tokens.RefreshToken, cancellationToken);
            return entry.Tokens;
        }
        finally
        {
            entry.Gate.Release();
        }
    }

    /// <summary>Forgets the session's tokens (sign-out).</summary>
    public void Remove(string session) => cache.Remove(Key(session));

    private SessionEntry Entry(string session)
    {
        lock (_create)
        {
            return cache.GetOrCreate(Key(session), item =>
            {
                item.SlidingExpiration = IdleTimeout;
                return new SessionEntry();
            })!;
        }
    }

    private static string Key(string session) => "personet.tokens." + session;

    private sealed class SessionEntry
    {
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public TokenSet? Tokens { get; set; }
    }
}
