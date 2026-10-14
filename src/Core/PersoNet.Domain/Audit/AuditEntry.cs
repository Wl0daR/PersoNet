using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Audit;

/// <summary>What happened to an audited record.</summary>
public enum AuditAction
{
    Created = 0,
    Changed = 1,
    Deleted = 2,
}

/// <summary>
/// One change of company data: who made it, when, to which record, and the values before and after. Written in the same
/// transaction as the change itself and never changed afterwards (the database refuses updates and deletes).
/// </summary>
public sealed class AuditEntry : Entity, ITenantOwned
{
    // Used by EF Core.
    private AuditEntry()
    {
        EntityType = string.Empty;
        UserName = string.Empty;
        Changes = "[]";
        State = "{}";
    }

    private AuditEntry(Guid tenantId, DateTimeOffset occurredAt, Guid? userId, string userName, string entityType, Guid entityId, Guid? employeeId, AuditAction action, string changes, string state)
    {
        TenantId = tenantId;
        OccurredAt = occurredAt;
        UserId = userId;
        UserName = userName;
        EntityType = entityType;
        EntityId = entityId;
        EmployeeId = employeeId;
        Action = action;
        Changes = changes;
        State = state;
    }

    public Guid TenantId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>The account that made the change; null for the system itself (seeding, background jobs).</summary>
    public Guid? UserId { get; private set; }

    /// <summary>The name of that account at the time, so the history reads well after the account is gone.</summary>
    public string UserName { get; private set; }

    /// <summary>The kind of record, e.g. "Employee" or "LeaveRequest".</summary>
    public string EntityType { get; private set; }

    public Guid EntityId { get; private set; }

    /// <summary>The employee the record belongs to, so an employee's card can show its whole history.</summary>
    public Guid? EmployeeId { get; private set; }

    public AuditAction Action { get; private set; }

    /// <summary>JSON list of the changed properties with their old and new values.</summary>
    public string Changes { get; private set; }

    /// <summary>JSON object with all values of the record after the change (before it, for a deletion).</summary>
    public string State { get; private set; }

    public static AuditEntry Record(
        Guid tenantId, DateTimeOffset occurredAt, Guid? userId, string userName, string entityType, Guid entityId, Guid? employeeId, AuditAction action, string changes, string state)
    {
        if (tenantId == Guid.Empty || entityId == Guid.Empty || string.IsNullOrWhiteSpace(entityType))
        {
            throw new DomainException("An audit entry needs a tenant and a record.");
        }

        return new AuditEntry(tenantId, occurredAt, userId, string.IsNullOrWhiteSpace(userName) ? "System" : userName, entityType, entityId, employeeId, action, changes, state);
    }
}
