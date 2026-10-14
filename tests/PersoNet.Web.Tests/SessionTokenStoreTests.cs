using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Time.Testing;
using PersoNet.Web.Authentication;

namespace PersoNet.Web.Tests;

public sealed class SessionTokenStoreTests : IDisposable
{
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 14, 9, 0, 0, TimeSpan.Zero));
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly FakeRefresher _refresher;
    private readonly SessionTokenStore _store;

    public SessionTokenStoreTests()
    {
        _refresher = new FakeRefresher(_time);
        _store = new SessionTokenStore(_cache, _refresher, _time);
    }

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task GetFresh_ValidCookieTokens_UsesThemWithoutRefreshing()
    {
        var cookie = Tokens("a1", "r1", minutes: 10);

        var tokens = await _store.GetFreshAsync("s", cookie, TestContext.Current.CancellationToken);

        tokens.ShouldBe(cookie);
        _refresher.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetFresh_TokenAboutToExpire_RefreshesOnceAndKeepsTheNewTokens()
    {
        var cookie = Tokens("a1", "r1", minutes: 10);
        await _store.GetFreshAsync("s", cookie, TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(9.5));

        var refreshed = await _store.GetFreshAsync("s", fromCookie: null, TestContext.Current.CancellationToken);
        var again = await _store.GetFreshAsync("s", fromCookie: null, TestContext.Current.CancellationToken);

        refreshed!.AccessToken.ShouldBe("access-1");
        refreshed.RefreshToken.ShouldBe("refresh-1");
        again.ShouldBe(refreshed);
        _refresher.Calls.ShouldBe(["r1"]);
    }

    [Fact]
    public async Task GetFresh_AfterRotation_IgnoresTheOlderCookieTokens()
    {
        // The circuit refreshed (r1 is now used up); a page request still carries the old cookie.
        var cookie = Tokens("a1", "r1", minutes: 10);
        await _store.GetFreshAsync("s", cookie, TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(10));
        await _store.GetFreshAsync("s", fromCookie: null, TestContext.Current.CancellationToken);

        var tokens = await _store.GetFreshAsync("s", cookie, TestContext.Current.CancellationToken);

        tokens!.RefreshToken.ShouldBe("refresh-1");
        _refresher.Calls.ShouldBe(["r1"]);
    }

    [Fact]
    public async Task GetFresh_RefreshRefused_ReturnsNull()
    {
        _refresher.Refuse = true;
        var cookie = Tokens("a1", "r1", minutes: 0);

        var tokens = await _store.GetFreshAsync("s", cookie, TestContext.Current.CancellationToken);

        tokens.ShouldBeNull();
    }

    [Fact]
    public async Task GetFresh_UnknownSessionWithoutCookie_ReturnsNull()
    {
        var tokens = await _store.GetFreshAsync("unknown", fromCookie: null, TestContext.Current.CancellationToken);

        tokens.ShouldBeNull();
    }

    [Fact]
    public async Task GetFresh_ConcurrentCallsOnExpiredToken_RefreshOnlyOnce()
    {
        _refresher.Delay = TimeSpan.FromMilliseconds(50);
        var cookie = Tokens("a1", "r1", minutes: 0);

        var results = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _store.GetFreshAsync("s", cookie, TestContext.Current.CancellationToken)));

        results.Select(tokens => tokens!.AccessToken).Distinct().ShouldBe(["access-1"]);
        _refresher.Calls.ShouldBe(["r1"]);
    }

    [Fact]
    public async Task Remove_ForgetsTheSession()
    {
        await _store.GetFreshAsync("s", Tokens("a1", "r1", minutes: 10), TestContext.Current.CancellationToken);

        _store.Remove("s");

        (await _store.GetFreshAsync("s", fromCookie: null, TestContext.Current.CancellationToken)).ShouldBeNull();
    }

    private TokenSet Tokens(string access, string refresh, double minutes) =>
        new(access, refresh, _time.GetUtcNow().AddMinutes(minutes));

    private sealed class FakeRefresher(TimeProvider time) : ITokenRefresher
    {
        private int _count;

        public List<string> Calls { get; } = [];

        public bool Refuse { get; set; }

        public TimeSpan Delay { get; set; }

        public async Task<TokenSet?> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            lock (Calls)
            {
                Calls.Add(refreshToken);
            }

            if (Delay > TimeSpan.Zero)
            {
                await Task.Delay(Delay, cancellationToken);
            }

            if (Refuse)
            {
                return null;
            }

            var n = Interlocked.Increment(ref _count);
            return new TokenSet($"access-{n}", $"refresh-{n}", time.GetUtcNow().AddMinutes(15));
        }
    }
}
