using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Identity;
using PersoNet.Contracts;
using PersoNet.Identity.Data;
using PersoNet.Identity.Endpoints;
using PersoNet.Identity.Seeding;
using static OpenIddict.Abstractions.OpenIddictConstants;

// OpenID Connect server for all PersoNet apps: local accounts now, Entra ID federation later (see docs/adr/0004).
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var connectionString = builder.Configuration.GetConnectionString(IdentityDatabase.ConnectionStringName)
    ?? throw new InvalidOperationException($"Connection string '{IdentityDatabase.ConnectionStringName}' is not configured.");

builder.Services.AddDbContext<IdentityDbContext>(options => options.UseIdentityDatabase(connectionString));

builder.Services
    .AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.SignIn.RequireConfirmedEmail = false;

        options.Password.RequiredLength = 12;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireDigit = true;

        options.Lockout.AllowedForNewUsers = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);

        // Claims in the cookie use the same names as the tokens.
        options.ClaimsIdentity.UserIdClaimType = Claims.Subject;
        options.ClaimsIdentity.UserNameClaimType = Claims.Name;
        options.ClaimsIdentity.EmailClaimType = Claims.Email;
        options.ClaimsIdentity.RoleClaimType = Claims.Role;
    })
    .AddEntityFrameworkStores<IdentityDbContext>()
    .AddDefaultTokenProviders();

// Links to set a password (new accounts and resets alike) work for a week, so HR can pass them on.
builder.Services.Configure<DataProtectionTokenProviderOptions>(options => options.TokenLifespan = TimeSpan.FromDays(7));
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "personet.identity";
    options.LoginPath = "/account/login";
    options.LogoutPath = "/account/logout";
    options.AccessDeniedPath = "/account/login";
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

builder.Services
    .AddOpenIddict()
    .AddCore(options => options.UseEntityFrameworkCore().UseDbContext<IdentityDbContext>().ReplaceDefaultEntities<Guid>())
    .AddServer(options =>
    {
        options
            .SetAuthorizationEndpointUris("connect/authorize")
            .SetTokenEndpointUris("connect/token")
            .SetEndSessionEndpointUris("connect/logout")
            .SetUserInfoEndpointUris("connect/userinfo");

        options
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow()
            .RequireProofKeyForCodeExchange();

        options.RegisterScopes(Scopes.OpenId, Scopes.Email, Scopes.Profile, Scopes.OfflineAccess, PersoNetAuth.ApiScope);

        options.SetAccessTokenLifetime(TimeSpan.FromMinutes(15));
        options.SetRefreshTokenLifetime(TimeSpan.FromDays(7));

        // Access tokens are signed but not encrypted, so the API can validate them with the published keys only.
        options.DisableAccessTokenEncryption();

        if (builder.Environment.IsDevelopment())
        {
            options
                .AddDevelopmentEncryptionCertificate()
                .AddDevelopmentSigningCertificate();
        }
        else
        {
            // On the server the certificates live in the machine store (LocalMachine\My), referenced by thumbprint.
            var certificates = builder.Configuration.GetSection("Identity:Certificates");
            options
                .AddEncryptionCertificate(RequiredThumbprint(certificates, "Encryption"), StoreName.My, StoreLocation.LocalMachine)
                .AddSigningCertificate(RequiredThumbprint(certificates, "Signing"), StoreName.My, StoreLocation.LocalMachine);
        }

        options
            .UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableEndSessionEndpointPassthrough()
            .EnableUserInfoEndpointPassthrough()
            .EnableStatusCodePagesIntegration();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

builder.Services.AddAuthorization();
builder.Services.AddRazorPages();

builder.Services.AddOptions<WebClientOptions>().BindConfiguration(WebClientOptions.SectionName);
builder.Services.AddOptions<ApiDocsClientOptions>().BindConfiguration(ApiDocsClientOptions.SectionName);

// Only the API documentation calls the token endpoint from another origin; the apps exchange codes server side.
const string ApiDocsCors = "api-docs";
var apiDocsOrigin = builder.Configuration[$"{ApiDocsClientOptions.SectionName}:Origin"];
builder.Services.AddCors(options => options.AddPolicy(ApiDocsCors, policy =>
{
    if (!string.IsNullOrEmpty(apiDocsOrigin))
    {
        policy.WithOrigins(apiDocsOrigin).AllowAnyHeader().WithMethods(HttpMethods.Post);
    }
}));

builder.Services.AddHealthChecks().AddDbContextCheck<IdentityDbContext>("database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    // Deployments run migrations as a separate step (see docs/adr/0005).
    await app.Services.SeedDevelopmentAsync().ConfigureAwait(false);
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

await app.Services.SyncClientsAsync().ConfigureAwait(false);

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors(ApiDocsCors);
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
app.MapConnectEndpoints();
app.MapAccountAdminEndpoints();
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);

static string RequiredThumbprint(IConfiguration section, string name) =>
    section[$"{name}Thumbprint"]
    ?? throw new InvalidOperationException($"Configure Identity:Certificates:{name}Thumbprint.");

