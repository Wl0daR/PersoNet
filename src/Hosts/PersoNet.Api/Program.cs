using OpenIddict.Validation.AspNetCore;
using PersoNet.Api.Account;
using PersoNet.Api.Development;
using PersoNet.Api.Employees;
using PersoNet.Api.OpenApi;
using PersoNet.Api.Organization;
using PersoNet.Api.Security;
using PersoNet.Api.Tenancy;
using PersoNet.Application;
using PersoNet.Application.Abstractions;
using PersoNet.Contracts;
using PersoNet.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();

// Access tokens come from PersoNet.Identity. Locally its address comes from Aspire service discovery;
// on IIS it is set in "Identity:Authority".
var authority = builder.Configuration["Identity:Authority"]
    ?? builder.Configuration["services:identity:https:0"]
    ?? throw new InvalidOperationException("Configure Identity:Authority (the PersoNet.Identity address).");

builder.Services
    .AddOpenIddict()
    .AddValidation(options =>
    {
        options.SetIssuer(authority);
        options.AddAudiences(PersoNetAuth.ApiResource);
        options.UseSystemNetHttp();
        options.UseAspNetCore();
    });

builder.Services.AddAuthentication(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);

var authorityUri = new Uri(authority.EndsWith('/') ? authority : authority + "/");
builder.Services.AddOpenApi("v1", options => options.AddDocumentTransformer(new OAuthSecuritySchemeTransformer(authorityUri)));
builder.Services.AddAuthorizationBuilder().AddPermissionPolicies();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Deployments run migrations as a separate step (see docs/adr/0005).
    await app.Services.MigrateDatabaseAsync().ConfigureAwait(false);
    await app.Services.SeedDevelopmentDataAsync().ConfigureAwait(false);

    app.MapOpenApi();
    // "Authorize" in the documentation signs in through PersoNet.Identity, like the apps do.
    app.MapScalarApiReference(options => options
        .AddPreferredSecuritySchemes(OAuthSecuritySchemeTransformer.SchemeName)
        .AddAuthorizationCodeFlow(OAuthSecuritySchemeTransformer.SchemeName, flow => flow
            .WithClientId(PersoNetAuth.ApiDocsClientId)
            .WithPkce(Pkce.Sha256)
            .WithRedirectUri(app.Configuration["ApiDocs:RedirectUri"] ?? "https://localhost:7016/scalar/")
            .WithSelectedScopes(["openid", "profile", "email", PersoNetAuth.ApiScope]))
        .EnablePersistentAuthentication());
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

// Module endpoints are mapped under this group (see "Konwencje API" in the plan). Every endpoint requires a signed-in user.
var api = app.MapGroup("/api/v1").RequireAuthorization();
api.MapAccountEndpoints();
api.MapOrganizationEndpoints();
api.MapEmployeeEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Entry point marker used by integration tests.
/// </summary>
public partial class Program;
