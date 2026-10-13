using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using PersoNet.Identity.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace PersoNet.Identity.Seeding;

internal static class IdentitySeeder
{
    /// <summary>
    /// Registers or updates the OpenID Connect clients from configuration. Runs on every start.
    /// </summary>
    public static async Task SyncClientsAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var web = scope.ServiceProvider.GetRequiredService<IOptions<WebClientOptions>>().Value;

        if (web.RedirectUri is null || string.IsNullOrEmpty(web.ClientSecret))
        {
            throw new InvalidOperationException($"Configure {WebClientOptions.SectionName} (RedirectUri, ClientSecret).");
        }

        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = PersoNetAuth.WebClientId,
            ClientSecret = web.ClientSecret,
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "PersoNet Web",
            RedirectUris = { web.RedirectUri },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.EndSession,
                Permissions.Endpoints.Token,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Permissions.Scopes.Email,
                Permissions.Scopes.Profile,
                Permissions.Prefixes.Scope + PersoNetAuth.ApiScope,
            },
            Requirements = { Requirements.Features.ProofKeyForCodeExchange },
        };

        if (web.PostLogoutRedirectUri is not null)
        {
            descriptor.PostLogoutRedirectUris.Add(web.PostLogoutRedirectUri);
        }

        var existing = await manager.FindByClientIdAsync(PersoNetAuth.WebClientId, cancellationToken);
        if (existing is null)
        {
            await manager.CreateAsync(descriptor, cancellationToken);
        }
        else
        {
            await manager.UpdateAsync(existing, descriptor, cancellationToken);
        }
    }

    /// <summary>
    /// Development only: applies migrations and creates the demo account.
    /// </summary>
    public static async Task SeedDevelopmentAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        if (await users.FindByEmailAsync(PersoNetAuth.DevelopmentSeed.UserEmail) is not null)
        {
            return;
        }

        var user = new ApplicationUser
        {
            UserName = PersoNetAuth.DevelopmentSeed.UserEmail,
            Email = PersoNetAuth.DevelopmentSeed.UserEmail,
            EmailConfirmed = true,
            DisplayName = "Administrator demo",
            TenantId = PersoNetAuth.DevelopmentSeed.TenantId,
        };

        var result = await users.CreateAsync(user, PersoNetAuth.DevelopmentSeed.UserPassword);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                "Could not create the demo account: " + string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
