using PersoNet.Application.Abstractions;

namespace PersoNet.Api.Security;

/// <summary>
/// Reads the caller from the access token issued by PersoNet.Identity.
/// </summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public Guid UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(PersoNetClaims.Subject)?.Value, out var userId)
            ? userId
            : throw new InvalidOperationException("The request has no authenticated user.");

    public string? Email => httpContextAccessor.HttpContext?.User.FindFirst(PersoNetClaims.Email)?.Value;
}
