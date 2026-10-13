using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace PersoNet.Api.OpenApi;

/// <summary>
/// Describes sign-in through PersoNet.Identity (authorization code + PKCE) in the OpenAPI document,
/// so the interactive documentation can get a token and call the API.
/// </summary>
internal sealed class OAuthSecuritySchemeTransformer(Uri authority) : IOpenApiDocumentTransformer
{
    public const string SchemeName = "personet";

    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);
        document.Components.SecuritySchemes[SchemeName] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.OAuth2,
            Description = "Logowanie przez PersoNet.Identity",
            Flows = new OpenApiOAuthFlows
            {
                AuthorizationCode = new OpenApiOAuthFlow
                {
                    AuthorizationUrl = new Uri(authority, "connect/authorize"),
                    TokenUrl = new Uri(authority, "connect/token"),
                    Scopes = new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["openid"] = "Tożsamość",
                        ["profile"] = "Imię i nazwisko",
                        ["email"] = "E-mail",
                        [PersoNetAuth.ApiScope] = "Dostęp do API PersoNet",
                    },
                },
            },
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference(SchemeName, document)] = [],
        });

        return Task.CompletedTask;
    }
}
