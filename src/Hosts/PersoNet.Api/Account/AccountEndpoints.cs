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
            .WithSummary("Zalogowany użytkownik");

        return api;
    }

    private static IResult GetCurrentUser(ClaimsPrincipal principal, ICurrentUser currentUser, ITenantContext tenant) =>
        tenant.TenantId is { } tenantId
            ? TypedResults.Ok(new CurrentUserResponse(
                currentUser.UserId,
                tenantId,
                principal.FindFirst(PersoNetClaims.Name)?.Value,
                principal.FindFirst(PersoNetClaims.Email)?.Value))
            : TypedResults.Forbid();
}
