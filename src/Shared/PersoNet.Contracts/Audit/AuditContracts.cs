using System.Text.Json.Serialization;

namespace PersoNet.Contracts.Audit;

/// <summary>What happened to a record in the history.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<HistoryAction>))]
public enum HistoryAction
{
    Created = 0,
    Changed = 1,
    Deleted = 2,
}

/// <summary>
/// One entry of an employee's history: who did what, and when. <see cref="Subject"/> names the kind of record ("Umowa"),
/// <see cref="Description"/> says what happened in a sentence, and <see cref="Changes"/> lists the fields before and after.
/// </summary>
public sealed record HistoryEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string UserName,
    string Subject,
    string Description,
    HistoryAction Action,
    IReadOnlyList<HistoryFieldChange> Changes);

/// <summary>A field of the record with its value before and after the change, both readable text (null when empty).</summary>
public sealed record HistoryFieldChange(string Field, string? OldValue, string? NewValue);
