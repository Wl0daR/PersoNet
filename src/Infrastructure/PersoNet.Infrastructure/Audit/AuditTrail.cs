using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Audit;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Audit;
using PersoNet.Domain.Employees;

namespace PersoNet.Infrastructure.Audit;

/// <summary>
/// Turns the pending changes of company data into <see cref="AuditEntry"/> rows, saved in the same transaction, so no
/// change is stored without its record of who made it.
/// </summary>
internal static class AuditTrail
{
    // Keys and technical columns say nothing to a reader of the history.
    private static readonly HashSet<string> Skipped = new(StringComparer.Ordinal) { "Id", "TenantId" };

    public static List<AuditEntry> Collect(ChangeTracker changes, IAuditActor actor, DateTimeOffset now)
    {
        var entries = new List<AuditEntry>();

        foreach (var entry in changes.Entries<ITenantOwned>())
        {
            if (entry.Entity is AuditEntry || entry.Entity is not Entity entity
                || entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var action = entry.State switch
            {
                EntityState.Added => AuditAction.Created,
                EntityState.Deleted => AuditAction.Deleted,
                _ => AuditAction.Changed,
            };

            var changed = new List<AuditChange>();
            var state = new SortedDictionary<string, string?>(StringComparer.Ordinal);

            foreach (var property in entry.Properties)
            {
                var name = property.Metadata.Name;
                if (Skipped.Contains(name) || property.Metadata.IsShadowProperty())
                {
                    continue;
                }

                var current = Format(property.CurrentValue);
                var original = Format(property.OriginalValue);
                state[name] = action == AuditAction.Deleted ? original : current;

                switch (action)
                {
                    case AuditAction.Created when current is not null:
                        changed.Add(new AuditChange(name, null, current));
                        break;
                    case AuditAction.Deleted when original is not null:
                        changed.Add(new AuditChange(name, original, null));
                        break;
                    case AuditAction.Changed when property.IsModified && original != current:
                        changed.Add(new AuditChange(name, original, current));
                        break;
                }
            }

            // Saving a record without a real difference is not a change.
            if (action == AuditAction.Changed && changed.Count == 0)
            {
                continue;
            }

            entries.Add(AuditEntry.Record(
                entry.Entity.TenantId,
                now,
                actor.UserId,
                actor.Name,
                entity.GetType().Name,
                entity.Id,
                EmployeeOf(entry, entity),
                action,
                JsonSerializer.Serialize(changed, AuditJson.Options),
                JsonSerializer.Serialize(state, AuditJson.Options)));
        }

        return entries;
    }

    private static Guid? EmployeeOf(EntityEntry entry, Entity entity) =>
        entity is Employee ? entity.Id
            : entry.Metadata.FindProperty("EmployeeId") is not null ? entry.Property("EmployeeId").CurrentValue as Guid?
            : null;

    // Invariant text, so the stored values never depend on the server's culture.
    private static string? Format(object? value) => value switch
    {
        null => null,
        DateOnly date => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime time => time.ToString("O", CultureInfo.InvariantCulture),
        DateTimeOffset time => time.ToString("O", CultureInfo.InvariantCulture),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
