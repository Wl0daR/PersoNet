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

/// <summary>
/// The interactive API documentation (Scalar) as a public client, from "Identity:Clients:ApiDocs". Development only:
/// leave the section out on servers and the client is removed.
/// </summary>
public sealed class ApiDocsClientOptions
{
    public const string SectionName = "Identity:Clients:ApiDocs";

    public Uri? RedirectUri { get; set; }

    /// <summary>
    /// Origin of the page that calls the token endpoint from the browser (CORS), for example https://localhost:7016.
    /// </summary>
    public string? Origin { get; set; }
}
