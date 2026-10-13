using System.Collections.Frozen;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using PersoNet.Application.Abstractions;

namespace PersoNet.Api.Security;

/// <summary>
/// Which permissions each role grants. Fixed in code for now; HR will configure it per company later.
/// </summary>
internal static class RolePermissions
{
    private static readonly FrozenDictionary<string, FrozenSet<string>> Map = new Dictionary<string, FrozenSet<string>>
    {
        [PersoNetAuth.Roles.HrAdmin] = Permissions.All.ToFrozenSet(),
    }.ToFrozenDictionary();

    public static bool HasPermission(ClaimsPrincipal user, string permission) =>
        user.FindAll(PersoNetClaims.Role).Any(role => Map.TryGetValue(role.Value, out var granted) && granted.Contains(permission));

    /// <summary>
    /// Registers one authorization policy per permission, named after it: <c>RequireAuthorization(Permissions.X)</c>.
    /// </summary>
    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder authorization)
    {
        foreach (var permission in Permissions.All)
        {
            authorization.AddPolicy(permission, policy => policy
                .RequireAuthenticatedUser()
                .RequireAssertion(context => HasPermission(context.User, permission)));
        }

        return authorization;
    }
}
