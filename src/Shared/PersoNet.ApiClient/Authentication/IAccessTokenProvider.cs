namespace PersoNet.ApiClient.Authentication;

/// <summary>
/// Supplies the access token sent with every API call. Each app implements it with its own sign-in.
/// </summary>
public interface IAccessTokenProvider
{
    /// <summary>
    /// A valid access token, refreshed if needed, or null when nobody is signed in.
    /// </summary>
    Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Default for apps that have no signed-in user yet: requests go out without a token.
/// </summary>
internal sealed class NoAccessTokenProvider : IAccessTokenProvider
{
    public Task<string?> GetAccessTokenAsync(CancellationToken cancellationToken) => Task.FromResult<string?>(null);
}
