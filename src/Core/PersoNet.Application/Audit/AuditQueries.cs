using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Audit;
using PersoNet.Domain.Audit;

namespace PersoNet.Application.Audit;

/// <summary>Reads the audit log as a history a person can follow: Polish names, readable values, one sentence per change.</summary>
public sealed class AuditQueries(IAppDbContext db)
{
    public const int MaxEntries = 500;

    private static readonly AppError EmployeeNotFound =
        AppError.NotFound("employee.not_found", "Nie znaleziono pracownika.");

    // Fields that hold the id of another record; the history shows that record's name instead.
    private static readonly HashSet<string> References = new(StringComparer.Ordinal) { "DepartmentId", "PositionId", "ManagerId", "RoleId" };

    /// <summary>Changes of the employee's card, contracts, leave and portal account, newest first.</summary>
    public async Task<Result<IReadOnlyList<HistoryEntryResponse>>> GetEmployeeHistoryAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return EmployeeNotFound;
        }

        var entries = await db.AuditLog.AsNoTracking()
            .Where(entry => entry.EmployeeId == employeeId)
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Take(MaxEntries)
            .ToListAsync(cancellationToken);

        var parsed = entries
            .Select(entry => (
                Entry: entry,
                Changes: JsonSerializer.Deserialize<List<AuditChange>>(entry.Changes, AuditJson.Options) ?? [],
                State: JsonSerializer.Deserialize<Dictionary<string, string?>>(entry.State, AuditJson.Options) ?? []))
            .ToList();

        var roleIds = parsed
            .Where(item => item.Entry.EntityType == "RoleAssignment")
            .Select(item => Guid.TryParse(item.State.GetValueOrDefault("RoleId"), out var id) ? id : Guid.Empty);
        var names = await NamesAsync(parsed.SelectMany(item => item.Changes), roleIds, cancellationToken);

        return parsed.Select(item => ToResponse(item.Entry, item.Changes, item.State, names)).ToList();
    }

    /// <summary>Names of the departments, positions and managers the changes refer to, including ones no longer in use.</summary>
    private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<AuditChange> changes, IEnumerable<Guid> roleIds, CancellationToken cancellationToken)
    {
        var ids = changes
            .Where(change => References.Contains(change.Property))
            .SelectMany(change => new[] { change.Old, change.New })
            .Select(value => Guid.TryParse(value, out var id) ? id : (Guid?)null)
            .OfType<Guid>()
            .Concat(roleIds)
            .Where(id => id != Guid.Empty)
            .ToHashSet();

        if (ids.Count == 0)
        {
            return [];
        }

        var departments = await db.Departments.AsNoTracking()
            .Where(department => ids.Contains(department.Id))
            .Select(department => new { department.Id, department.Name })
            .ToListAsync(cancellationToken);
        var positions = await db.Positions.AsNoTracking()
            .Where(position => ids.Contains(position.Id))
            .Select(position => new { position.Id, position.Name })
            .ToListAsync(cancellationToken);
        var managers = await db.Employees.AsNoTracking()
            .Where(employee => ids.Contains(employee.Id))
            .Select(employee => new { employee.Id, Name = employee.FirstName + " " + employee.LastName })
            .ToListAsync(cancellationToken);

        var roles = await db.Roles.AsNoTracking()
            .Where(role => ids.Contains(role.Id))
            .Select(role => new { role.Id, role.Name })
            .ToListAsync(cancellationToken);

        return departments.Concat(positions).Concat(managers).Concat(roles).ToDictionary(item => item.Id, item => item.Name);
    }

    private static HistoryEntryResponse ToResponse(
        AuditEntry entry, List<AuditChange> changes, Dictionary<string, string?> state, Dictionary<Guid, string> names)
    {
        var byProperty = changes.ToDictionary(change => change.Property, StringComparer.Ordinal);

        var fields = AuditTexts.FieldsOf(entry.EntityType)
            .Where(field => byProperty.ContainsKey(field.Property))
            .Select(field =>
            {
                var change = byProperty[field.Property];
                return new HistoryFieldChange(field.Label, Readable(field.Property, change.Old, names), Readable(field.Property, change.New, names));
            })
            .ToList();

        return new HistoryEntryResponse(
            entry.Id,
            entry.OccurredAt,
            entry.UserName,
            AuditTexts.Subject(entry.EntityType),
            Describe(entry.EntityType, entry.Action, byProperty, state, names),
            (HistoryAction)entry.Action,
            fields);
    }

    private static string? Readable(string property, string? stored, Dictionary<Guid, string> names) =>
        References.Contains(property) && Guid.TryParse(stored, out var id)
            ? names.GetValueOrDefault(id, "(usunięty)")
            : AuditTexts.Value(property, stored);

    private static string Describe(
        string entityType, AuditAction action, Dictionary<string, AuditChange> changes, Dictionary<string, string?> state, Dictionary<Guid, string> names)
    {
        string? Changed(string property) => changes.TryGetValue(property, out var change) ? change.New : null;
        bool Has(string property) => changes.ContainsKey(property);
        string? Value(string property) => AuditTexts.Value(property, state.GetValueOrDefault(property));

        return entityType switch
        {
            "Employee" => action switch
            {
                AuditAction.Created => "Założono kartotekę",
                AuditAction.Deleted => "Usunięto kartotekę",
                _ when Has("HireDate") && Has("TerminationDate") && Changed("TerminationDate") is null => $"Zatrudniono ponownie od {Value("HireDate")}",
                _ when Has("TerminationDate") && Changed("TerminationDate") is not null => $"Zakończono zatrudnienie z dniem {Value("TerminationDate")}",
                _ when Has("TerminationDate") => "Cofnięto zakończenie zatrudnienia",
                _ => "Zmieniono dane",
            },
            "EmploymentContract" => action switch
            {
                AuditAction.Created => $"Dodano umowę: {Value("Type")} od {Value("StartDate")}",
                AuditAction.Deleted => $"Usunięto umowę: {Value("Type")} od {Value("StartDate")}",
                _ when changes.Count == 1 && Has("TerminatedOn") && Changed("TerminatedOn") is not null =>
                    $"Umowa rozwiązana z dniem {Value("TerminatedOn")}: {Value("Type")}",
                _ when changes.Count == 1 && Has("TerminatedOn") => $"Umowa znów trwa do planowanego końca: {Value("Type")}",
                _ => $"Zmieniono umowę: {Value("Type")} od {Value("StartDate")}",
            },
            "LeaveRequest" => (action switch
            {
                AuditAction.Created => "Złożono wniosek",
                AuditAction.Deleted => "Usunięto wniosek",
                _ => Changed("Status") switch
                {
                    "Approved" => "Zatwierdzono wniosek",
                    "Rejected" => "Odrzucono wniosek",
                    "Cancelled" => "Anulowano wniosek",
                    _ => "Zmieniono wniosek",
                },
            }) + $": {Value("Type")}, {Period(Value("StartDate"), Value("EndDate"))}",
            "LeaveEntitlement" => action switch
            {
                AuditAction.Created => $"Ustawiono wymiar urlopu na {state.GetValueOrDefault("Year")}",
                AuditAction.Deleted => $"Usunięto wymiar urlopu na {state.GetValueOrDefault("Year")}",
                _ => $"Zmieniono wymiar urlopu na {state.GetValueOrDefault("Year")}",
            },
            AuditTexts.AccountEntity => state.GetValueOrDefault(AuditTexts.AccountEventProperty) switch
            {
                AuditTexts.AccountEventBlock => "Zablokowano konto w portalu",
                AuditTexts.AccountEventUnblock => "Odblokowano konto w portalu",
                _ when action == AuditAction.Created => "Utworzono konto w portalu i link do ustawienia hasła",
                _ => "Wygenerowano nowy link do ustawienia hasła",
            },
            "RoleAssignment" => (action == AuditAction.Deleted ? "Odebrano rolę: " : "Nadano rolę: ")
                + Readable("RoleId", state.GetValueOrDefault("RoleId"), names),
            "Role" => action switch
            {
                AuditAction.Created => $"Dodano rolę: {state.GetValueOrDefault("Name")}",
                AuditAction.Deleted => $"Usunięto rolę: {state.GetValueOrDefault("Name")}",
                _ => $"Zmieniono rolę: {state.GetValueOrDefault("Name")}",
            },
            _ => action switch
            {
                AuditAction.Created => "Dodano",
                AuditAction.Deleted => "Usunięto",
                _ => "Zmieniono",
            },
        };
    }

    private static string Period(string? start, string? end) =>
        start == end ? start ?? string.Empty : $"{start} – {end}";
}
