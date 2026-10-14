using System.Security.Claims;
using PersoNet.Api.Security;
using PersoNet.Application.Abstractions;
using PersoNet.Contracts.Account;

namespace PersoNet.Api.Account;

internal static class AccountEndpoints
{
    public static RouteGroupBuilder MapAccountEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/me", GetCurrentUser)
            .WithName("GetCurrentUser")
            .WithTags("Konto")
            .WithSummary("Zalogowany użytkownik i jego uprawnienia");

        return api;
    }

    private static async Task<IResult> GetCurrentUser(
        ClaimsPrincipal principal, ICurrentUser currentUser, ITenantContext tenant, CallerPermissions permissions, CancellationToken cancellationToken) =>
        tenant.TenantId is { } tenantId
            ? TypedResults.Ok(new CurrentUserResponse(
                currentUser.UserId,
                tenantId,
                principal.FindFirst(PersoNetClaims.Name)?.Value,
                principal.FindFirst(PersoNetClaims.Email)?.Value,
                [.. (await permissions.GetAsync(cancellationToken)).Order(StringComparer.Ordinal)]))
            : TypedResults.Forbid();
}
