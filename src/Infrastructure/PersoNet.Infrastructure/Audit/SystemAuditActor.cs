using PersoNet.Application.Abstractions;

namespace PersoNet.Infrastructure.Audit;

/// <summary>Changes made without a signed-in user (seeding, background jobs) are recorded as the system's.</summary>
public sealed class SystemAuditActor : IAuditActor
{
    public Guid? UserId => null;

    public string Name => "System";
}
