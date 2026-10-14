namespace PersoNet.Application.Abstractions;

/// <summary>Who changes data right now, for the audit trail; the system itself when nobody is signed in.</summary>
public interface IAuditActor
{
    Guid? UserId { get; }

    /// <summary>A readable name: the account's name or e-mail, or "System".</summary>
    string Name { get; }
}
