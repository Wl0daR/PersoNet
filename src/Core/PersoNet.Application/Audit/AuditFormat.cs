using System.Text.Json;

namespace PersoNet.Application.Audit;

/// <summary>One changed property in <see cref="Domain.Audit.AuditEntry.Changes"/>, as invariant text.</summary>
public sealed record AuditChange(string Property, string? Old, string? New);

/// <summary>The JSON format of <see cref="Domain.Audit.AuditEntry.Changes"/> and <see cref="Domain.Audit.AuditEntry.State"/>.</summary>
public static class AuditJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}
