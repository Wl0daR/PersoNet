using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Access;
using PersoNet.Contracts;

namespace PersoNet.Api.Security;

/// <summary>
/// Which permissions the caller has: all of them for the company's HR administrator (role in the access token), otherwise
/// those of the roles the company gave the caller's employee record. Worked out once per request.
/// </summary>
internal sealed class CallerPermissions(IHttpContextAccessor httpContextAccessor, ITenantContext tenant, PermissionLookup lookup)
{
    private static readonly IReadOnlySet<string> None = new HashSet<string>();
    private static readonly IReadOnlySet<string> Everything = Permissions.All.ToHashSet(StringComparer.Ordinal);

    private IReadOnlySet<string>? _granted;

    public async Task<IReadOnlySet<string>> GetAsync(CancellationToken cancellationToken)
    {
        if (_granted is not null)
        {
            return _granted;
        }

        var user = httpContextAccessor.HttpContext?.User;
        _granted = user?.Identity?.IsAuthenticated != true || tenant.TenantId is null ? None
            : IsAdministrator(user) ? Everything
            : await lookup.ForEmailAsync(user.FindFirst(PersoNetClaims.Email)?.Value, cancellationToken);

        return _granted;
    }

    public static bool IsAdministrator(ClaimsPrincipal user) =>
        user.FindAll(PersoNetClaims.Role).Any(role => role.Value == PersoNetAuth.Roles.HrAdmin);
}

/// <summary>An endpoint needs this permission.</summary>
internal sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

internal sealed class PermissionHandler(CallerPermissions caller) : AuthorizationHandler<PermissionRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if ((await caller.GetAsync(CancellationToken.None)).Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }
}

internal static class RolePermissions
{
    /// <summary>
    /// Registers one authorization policy per permission, named after it: <c>RequireAuthorization(Permissions.X)</c>.
    /// </summary>
    public static IServiceCollection AddPermissionPolicies(this IServiceCollection services)
    {
        services.AddScoped<CallerPermissions>();
        services.AddScoped<IAuthorizationHandler, PermissionHandler>();

        var authorization = services.AddAuthorizationBuilder();
        foreach (var permission in Permissions.All)
        {
            authorization.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission)));
        }

        return services;
    }
}
