using PersoNet.Application.Abstractions;

namespace PersoNet.Api.Security;

/// <summary>
/// The signed-in caller as the author of changes in the audit trail; the system when there is no request (startup seeding).
/// </summary>
internal sealed class HttpAuditActor(IHttpContextAccessor httpContextAccessor) : IAuditActor
{
    public Guid? UserId =>
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirst(PersoNetClaims.Subject)?.Value, out var userId) ? userId : null;

    public string Name =>
        httpContextAccessor.HttpContext?.User is { } user
            ? user.FindFirst(PersoNetClaims.Name)?.Value ?? user.FindFirst(PersoNetClaims.Email)?.Value ?? "System"
            : "System";
}
