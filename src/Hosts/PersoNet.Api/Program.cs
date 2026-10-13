using OpenIddict.Validation.AspNetCore;
using PersoNet.Api.Account;
using PersoNet.Api.Development;
using PersoNet.Api.Organization;
using PersoNet.Api.Security;
using PersoNet.Api.Tenancy;
using PersoNet.Application;
using PersoNet.Application.Abstractions;
using PersoNet.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");
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
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultEndpoints();

// Module endpoints are mapped under this group (see "Konwencje API" in the plan). Every endpoint requires a signed-in user.
var api = app.MapGroup("/api/v1").RequireAuthorization();
api.MapAccountEndpoints();
api.MapOrganizationEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Entry point marker used by integration tests.
/// </summary>
public partial class Program;
