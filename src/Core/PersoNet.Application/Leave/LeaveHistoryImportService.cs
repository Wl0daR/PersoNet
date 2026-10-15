using System.Globalization;
using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Absences;
using PersoNet.Domain.Absences;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Leave;

namespace PersoNet.Application.Leave;

/// <summary>
/// Moves the leave history of an earlier system over to employees already imported, matched by personnel number: leave
/// taken (as approved requests, without the balance and limit checks of new ones, split at the turn of a year), absences,
/// and the yearly entitlements. What is already in PersoNet is left alone, so the same files can be imported again.
/// </summary>
public sealed class LeaveHistoryImportService(IAppDbContext db, ITenantContext tenant, ICurrentUser user, TimeProvider time)
{
    public const int MaxRows = 5000;

    /// <summary>The decision note of imported leave, so it can be told apart from requests decided in PersoNet.</summary>
    public const string ImportNote = "Import z poprzedniego systemu";

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public async Task<Result<ImportLeaveHistoryResponse>> ImportAsync(ImportLeaveHistoryRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return LeaveErrors.NoTenant;
        }

        var leaveRows = request.Leave ?? [];
        var absenceRows = request.Absences ?? [];
        var entitlementRows = request.Entitlements ?? [];
        if (leaveRows.Count + absenceRows.Count + entitlementRows.Count == 0)
        {
            return LeaveErrors.ImportEmpty;
        }

        if (leaveRows.Count > MaxRows || absenceRows.Count > MaxRows || entitlementRows.Count > MaxRows)
        {
            return LeaveErrors.ImportTooLarge;
        }

        var numbers = leaveRows.Select(row => row.Number)
            .Concat(absenceRows.Select(row => row.Number))
            .Concat(entitlementRows.Select(row => row.Number))
            .Select(Number).Distinct().ToList();
        var employees = await db.Employees.AsNoTracking()
            .Where(employee => numbers.Contains(employee.Number))
            .ToDictionaryAsync(employee => employee.Number, employee => employee.Id, StringComparer.Ordinal, cancellationToken);
        var ids = employees.Values.ToList();

        var history = new History(
            (await db.LeaveRequests.AsNoTracking()
                .Where(leave => ids.Contains(leave.EmployeeId) && (leave.Status == LeaveStatus.Pending || leave.Status == LeaveStatus.Approved))
                .Select(leave => new { leave.EmployeeId, leave.Type, leave.StartDate, leave.EndDate })
                .ToListAsync(cancellationToken))
                .Select(leave => new Period(leave.EmployeeId, (int)leave.Type, leave.StartDate, leave.EndDate))
                .ToLookup(period => period.EmployeeId).ToDictionary(group => group.Key, group => group.ToList()),
            (await db.Absences.AsNoTracking()
                .Where(absence => ids.Contains(absence.EmployeeId))
                .Select(absence => new { absence.EmployeeId, absence.Type, absence.StartDate, absence.EndDate })
                .ToListAsync(cancellationToken))
                .Select(absence => new Period(absence.EmployeeId, (int)absence.Type, absence.StartDate, absence.EndDate))
                .ToLookup(period => period.EmployeeId).ToDictionary(group => group.Key, group => group.ToList()),
            (await db.Contracts.AsNoTracking()
                .Where(contract => ids.Contains(contract.EmployeeId))
                .Select(contract => new WorkingTime(contract.EmployeeId, contract.StartDate, contract.TerminatedOn ?? contract.EndDate, contract.WorkingTime))
                .ToListAsync(cancellationToken))
                .ToLookup(contract => contract.EmployeeId));

        var problems = new List<ImportLeaveProblem>();
        var now = time.GetUtcNow();
        int leaveAdded = 0, absencesAdded = 0, saved = 0, existing = 0;

        foreach (var row in leaveRows)
        {
            var number = Number(row.Number);
            if (Check(employees, number, row.StartDate, row.EndDate) is { } problem)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, problem));
                continue;
            }

            var employeeId = employees[number];
            var type = (LeaveType)row.Kind;
            if (!Enum.IsDefined(type))
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, "Nieznany rodzaj urlopu."));
                continue;
            }

            var parts = SplitByYear(row.StartDate, row.EndDate).Where(part => PolishCalendar.WorkingDays(part.Start, part.End) > 0).ToList();
            if (parts.Count == 0)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, $"Urlop {Span(row.StartDate, row.EndDate)} nie obejmuje żadnego dnia roboczego."));
                continue;
            }

            var taken = history.LeaveOf(employeeId);
            foreach (var (start, end) in parts)
            {
                if (taken.Any(other => other.Kind == (int)type && other.Start == start && other.End == end))
                {
                    existing++;
                    continue;
                }

                if (taken.FirstOrDefault(other => other.Start <= end && start <= other.End) is { } overlapping)
                {
                    problems.Add(new ImportLeaveProblem(row.Line, number, $"Urlop {Span(start, end)} nakłada się na inny urlop ({Span(overlapping.Start, overlapping.End)})."));
                    continue;
                }

                var leave = LeaveRequest.Create(tenantId, employeeId, type, start, end, history.DailyHours(employeeId, start), null, null, now);
                leave.Approve(user.UserId, now, ImportNote);
                db.LeaveRequests.Add(leave);
                taken.Add(new Period(employeeId, (int)type, start, end));
                leaveAdded++;
            }
        }

        foreach (var row in absenceRows)
        {
            var number = Number(row.Number);
            if (Check(employees, number, row.StartDate, row.EndDate) is { } problem)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, problem));
                continue;
            }

            var employeeId = employees[number];
            var recorded = history.AbsencesOf(employeeId);
            if (recorded.Any(other => other.Kind == (int)row.Kind && other.Start == row.StartDate && other.End == row.EndDate))
            {
                existing++;
                continue;
            }

            // The earlier system let absences overlap (home office on a day of a business trip), so they are kept as they were.
            try
            {
                db.Absences.Add(Absence.Create(tenantId, employeeId, (AbsenceType)row.Kind, row.StartDate, row.EndDate, row.Note, now));
                recorded.Add(new Period(employeeId, (int)row.Kind, row.StartDate, row.EndDate));
                absencesAdded++;
            }
            catch (DomainException)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, "Nieznany rodzaj nieobecności."));
            }
        }

        var entitlements = entitlementRows.Count == 0
            ? []
            : await db.LeaveEntitlements.Where(entitlement => ids.Contains(entitlement.EmployeeId)).ToListAsync(cancellationToken);
        foreach (var row in entitlementRows)
        {
            var number = Number(row.Number);
            if (!employees.TryGetValue(number, out var employeeId))
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, NoEmployee(number)));
                continue;
            }

            if (row.Days is < 0 or > LeaveEntitlement.MaxDays || row.CarriedOverDays is < 0 or > LeaveEntitlement.MaxDays || row.Year is < 2000 or > 2100)
            {
                problems.Add(new ImportLeaveProblem(row.Line, number, string.Create(Polish,
                    $"Wymiar urlopu na {row.Year} r. ({row.Days} dni, zaległy {row.CarriedOverDays}) musi mieć od 0 do {LeaveEntitlement.MaxDays} dni.")));
                continue;
            }

            var current = entitlements.FirstOrDefault(entitlement => entitlement.EmployeeId == employeeId && entitlement.Year == row.Year);
            if (current is null)
            {
                current = LeaveEntitlement.Create(tenantId, employeeId, row.Year, row.Days, row.CarriedOverDays);
                db.LeaveEntitlements.Add(current);
                entitlements.Add(current);
                saved++;
            }
            else if (current.Days == row.Days && current.CarriedOverDays == row.CarriedOverDays)
            {
                existing++;
            }
            else
            {
                current.Change(row.Days, row.CarriedOverDays);
                saved++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportLeaveHistoryResponse(leaveAdded, absencesAdded, saved, existing, problems);
    }

    /// <summary>The period cut at each turn of a year: 28.12–3.01 becomes 28.12–31.12 and 1.01–3.01.</summary>
    public static IReadOnlyList<(DateOnly Start, DateOnly End)> SplitByYear(DateOnly start, DateOnly end)
    {
        var parts = new List<(DateOnly, DateOnly)>();
        for (var from = start; from <= end; from = new DateOnly(from.Year + 1, 1, 1))
        {
            var yearEnd = new DateOnly(from.Year, 12, 31);
            parts.Add((from, end < yearEnd ? end : yearEnd));
            if (from.Year == DateOnly.MaxValue.Year)
            {
                break;
            }
        }

        return parts;
    }

    private static string? Check(Dictionary<string, Guid> employees, string number, DateOnly start, DateOnly end)
    {
        if (!employees.ContainsKey(number))
        {
            return NoEmployee(number);
        }

        return end < start ? $"Okres od {start.ToString("d", Polish)} kończy się przed początkiem ({end.ToString("d", Polish)})." : null;
    }

    private static string NoEmployee(string number) => $"Nie ma pracownika o numerze {number}; najpierw zaimportuj pracowników.";

    private static string Span(DateOnly start, DateOnly end) =>
        start == end ? start.ToString("d", Polish) : $"{start.ToString("d", Polish)}–{end.ToString("d", Polish)}";

    private static string Number(string? number) => number?.Trim().ToUpperInvariant() ?? string.Empty;

    private sealed record Period(Guid EmployeeId, int Kind, DateOnly Start, DateOnly End);

    private sealed record WorkingTime(Guid EmployeeId, DateOnly Start, DateOnly? End, decimal? Share);

    private sealed class History(Dictionary<Guid, List<Period>> leave, Dictionary<Guid, List<Period>> absences, ILookup<Guid, WorkingTime> contracts)
    {
        public List<Period> LeaveOf(Guid employeeId) => Of(leave, employeeId);

        public List<Period> AbsencesOf(Guid employeeId) => Of(absences, employeeId);

        /// <summary>8 hours times the working time of the contract on that day, as for new requests.</summary>
        public decimal DailyHours(Guid employeeId, DateOnly day)
        {
            var share = contracts[employeeId]
                .Where(contract => contract.Start <= day && (contract.End is null || contract.End >= day))
                .OrderByDescending(contract => contract.Start)
                .Select(contract => contract.Share)
                .FirstOrDefault();

            return Math.Round(LeaveEntitlement.HoursPerDay * (share ?? 1m), 2);
        }

        private static List<Period> Of(Dictionary<Guid, List<Period>> periods, Guid employeeId)
        {
            if (!periods.TryGetValue(employeeId, out var list))
            {
                periods[employeeId] = list = [];
            }

            return list;
        }
    }
}
