using PersoNet.ApiClient;
using PersoNet.ApiClient.Portal;
using PersoNet.Contracts.SelfService;

namespace PersoNet.Web.Portal;

/// <summary>
/// The signed-in person's employee record, loaded once per request or circuit and shared by the menu and the pages.
/// <see cref="Problem"/> explains why there is none, e.g. the account is not linked to an employee yet.
/// </summary>
internal sealed record MyProfileResult(MyProfileResponse? Profile, string? Problem)
{
    public bool IsManager => Profile?.TeamSize > 0;
}

internal sealed class MyProfile(IPortalApi portal)
{
    private Task<MyProfileResult>? _load;

    public Task<MyProfileResult> GetAsync(CancellationToken cancellationToken) => _load ??= LoadAsync(cancellationToken);

    private async Task<MyProfileResult> LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return new MyProfileResult(await portal.GetProfileAsync(cancellationToken), null);
        }
        catch (ApiException exception)
        {
            return new MyProfileResult(null, exception.Message);
        }
        catch (HttpRequestException)
        {
            return new MyProfileResult(null, Messages.ServerUnavailable);
        }
    }
}

/// <summary>Messages shared by the portal pages.</summary>
internal static class Messages
{
    public const string ServerUnavailable = "Nie udało się połączyć z serwerem PersoNet. Spróbuj ponownie za chwilę.";

    /// <summary>The user-facing text of a failed API call.</summary>
    public static string Of(Exception exception) => exception switch
    {
        ApiException api => api.Message,
        HttpRequestException => ServerUnavailable,
        _ => "Wystąpił nieoczekiwany błąd. Spróbuj ponownie.",
    };
}
