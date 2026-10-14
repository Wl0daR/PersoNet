using Microsoft.AspNetCore.Authentication;
using PersoNet.Web.Authentication;

namespace PersoNet.Web.Tests;

public sealed class TokenSetTests
{
    [Fact]
    public void WriteTo_ThenFrom_RoundTripsTheCookieTokens()
    {
        var properties = new AuthenticationProperties();
        properties.StoreTokens([new AuthenticationToken { Name = "access_token", Value = "old" }]);
        var tokens = new TokenSet("a", "r", new DateTimeOffset(2026, 10, 14, 9, 15, 0, TimeSpan.Zero));

        tokens.WriteTo(properties);

        TokenSet.From(properties).ShouldBe(tokens);
    }

    [Fact]
    public void From_NoAccessToken_ReturnsNull()
    {
        TokenSet.From(new AuthenticationProperties()).ShouldBeNull();
        TokenSet.From(null).ShouldBeNull();
    }
}
