using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Account;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CurrentUserEndpointTests(PersoNetApiFactory factory)
{
    private static readonly Uri Me = new("/api/v1/me", UriKind.Relative);

    [Fact]
    public async Task Me_without_token_returns_unauthorized()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Me, TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_returns_the_signed_in_user_and_company()
    {
        using var client = factory.CreateSignedInClient();

        var user = await client.GetFromJsonAsync<CurrentUserResponse>(Me, TestContext.Current.CancellationToken);

        user.ShouldNotBeNull();
        user.UserId.ShouldBe(TestAuthHandler.UserId);
        user.TenantId.ShouldBe(TestAuthHandler.TenantId);
        user.Name.ShouldBe("Anna Kowalska");
        user.Email.ShouldBe("anna@acme.test");
    }
}
