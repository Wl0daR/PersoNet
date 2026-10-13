namespace PersoNet.Identity.Seeding;

/// <summary>
/// The web app's registration as an OpenID Connect client, from the "Identity:Clients:Web" configuration section.
/// </summary>
public sealed class WebClientOptions
{
    public const string SectionName = "Identity:Clients:Web";

    public Uri? RedirectUri { get; set; }

    public Uri? PostLogoutRedirectUri { get; set; }

    public string? ClientSecret { get; set; }
}
