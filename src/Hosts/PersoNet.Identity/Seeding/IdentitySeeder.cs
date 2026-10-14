using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using PersoNet.Contracts;
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
        var apiDocs = scope.ServiceProvider.GetRequiredService<IOptions<ApiDocsClientOptions>>().Value;

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

        await UpsertAsync(manager, descriptor, cancellationToken);

        // The desktop app is a public client: it cannot keep a secret, so PKCE protects the code instead.
        var desktop = new OpenIddictApplicationDescriptor
        {
            ClientId = PersoNetAuth.DesktopClientId,
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit,
            DisplayName = "PersoNet Kadry (desktop)",
            Permissions =
            {
                Permissions.Endpoints.Authorization,
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

        foreach (var port in PersoNetAuth.DesktopCallbackPorts)
        {
            desktop.RedirectUris.Add(new Uri($"http://localhost:{port}/{PersoNetAuth.DesktopCallbackPath}"));
        }

        await UpsertAsync(manager, desktop, cancellationToken);

        // The interactive API documentation (Scalar) signs in from the browser, so it is a public client without a secret.
        // Registered only where configured (Development).
        if (apiDocs.RedirectUri is not null)
        {
            await UpsertAsync(manager, new OpenIddictApplicationDescriptor
            {
                ClientId = PersoNetAuth.ApiDocsClientId,
                ClientType = ClientTypes.Public,
                ConsentType = ConsentTypes.Implicit,
                DisplayName = "PersoNet API docs",
                RedirectUris = { apiDocs.RedirectUri },
                Permissions =
                {
                    Permissions.Endpoints.Authorization,
                    Permissions.Endpoints.Token,
                    Permissions.GrantTypes.AuthorizationCode,
                    Permissions.ResponseTypes.Code,
                    Permissions.Scopes.Email,
                    Permissions.Scopes.Profile,
                    Permissions.Prefixes.Scope + PersoNetAuth.ApiScope,
                },
                Requirements = { Requirements.Features.ProofKeyForCodeExchange },
            }, cancellationToken);
        }
        else if (await manager.FindByClientIdAsync(PersoNetAuth.ApiDocsClientId, cancellationToken) is { } stale)
        {
            await manager.DeleteAsync(stale, cancellationToken);
        }
    }

    private static async Task UpsertAsync(IOpenIddictApplicationManager manager, OpenIddictApplicationDescriptor descriptor, CancellationToken cancellationToken)
    {
        var existing = await manager.FindByClientIdAsync(descriptor.ClientId!, cancellationToken);
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
    /// Development only: applies migrations and creates the demo accounts (HR administrator, employee and manager).
    /// </summary>
    public static async Task SeedDevelopmentAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);

        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in PersoNetAuth.Roles.All)
        {
            if (!await roles.RoleExistsAsync(role))
            {
                EnsureSucceeded(await roles.CreateAsync(new IdentityRole<Guid>(role) { Id = Guid.CreateVersion7() }), "role " + role);
            }
        }

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await EnsureUserAsync(users, PersoNetAuth.DevelopmentSeed.UserEmail, "Administrator demo", PersoNetAuth.Roles.HrAdmin);

        // Employee and manager portal accounts, without HR rights.
        await EnsureUserAsync(users, PersoNetAuth.DevelopmentSeed.EmployeeEmail, "Pracownik demo", role: null);
        await EnsureUserAsync(users, PersoNetAuth.DevelopmentSeed.ManagerEmail, "Kierownik demo", role: null);
    }

    private static async Task EnsureUserAsync(UserManager<ApplicationUser> users, string email, string displayName, string? role)
    {
        var user = await users.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                TenantId = PersoNetAuth.DevelopmentSeed.TenantId,
            };

            EnsureSucceeded(await users.CreateAsync(user, PersoNetAuth.DevelopmentSeed.UserPassword), "demo account " + email);
        }

        // Accounts created before roles existed get their role too.
        if (role is not null && !await users.IsInRoleAsync(user, role))
        {
            EnsureSucceeded(await users.AddToRoleAsync(user, role), "demo account role");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string what)
    {
        if (!result.Succeeded)
        {
            throw new InvalidOperationException(
                $"Could not create the {what}: " + string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }
}
