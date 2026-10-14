using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;

namespace PersoNet.Application.Employees;

/// <summary>
/// Moves employees over from an earlier system: their cards, the end of employment and this year's leave entitlement.
/// A personnel number already in PersoNet is left as it is, so the same file can be imported again without duplicates;
/// a row that breaks a rule is reported and the rest are imported.
/// </summary>
public sealed class EmployeeImportService(IAppDbContext db, ITenantContext tenant, TimeProvider time)
{
    public const int MaxRows = 5000;

    public async Task<Result<ImportEmployeesResponse>> ImportAsync(ImportEmployeesRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return EmployeeErrors.NoTenant;
        }

        if (request.Rows is null || request.Rows.Count == 0)
        {
            return EmployeeErrors.ImportEmpty;
        }

        if (request.Rows.Count > MaxRows)
        {
            return EmployeeErrors.ImportTooLarge;
        }

        var year = time.GetLocalNow().Year;
        var yearStart = new DateOnly(year, 1, 1);
        var yearEnd = new DateOnly(year, 12, 31);

        var numbers = await db.Employees.AsNoTracking().Select(employee => employee.Number).ToListAsync(cancellationToken);
        var taken = numbers.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        var problems = new List<ImportRowProblem>();
        var added = 0;
        var existing = 0;
        var entitlements = 0;

        foreach (var row in request.Rows)
        {
            var number = row.Number?.Trim().ToUpperInvariant() ?? string.Empty;
            if (taken.Contains(number))
            {
                existing++;
                continue;
            }

            if (!seen.Add(number))
            {
                problems.Add(new ImportRowProblem(row.Line, number, "Ten numer ewidencyjny występuje w pliku więcej niż raz."));
                continue;
            }

            if (row.AnnualLeaveDays is < 0 or > LeaveEntitlement.MaxDays)
            {
                problems.Add(new ImportRowProblem(row.Line, number, $"Wymiar urlopu musi mieć od 0 do {LeaveEntitlement.MaxDays} dni."));
                continue;
            }

            Employee employee;
            try
            {
                employee = Employee.Create(
                    tenantId,
                    new EmployeeDetails(number, row.FirstName, row.LastName, null, row.Phone, row.HireDate, null, null, null));

                if (row.TerminationDate is { } lastDay)
                {
                    employee.Terminate(lastDay);
                }
            }
            catch (DomainException exception)
            {
                problems.Add(new ImportRowProblem(row.Line, number, Describe(exception)));
                continue;
            }

            db.Employees.Add(employee);
            added++;

            // The entitlement only matters to someone who works for the company at some point this year.
            if (row.AnnualLeaveDays is { } days && employee.HireDate <= yearEnd && (employee.TerminationDate is null || employee.TerminationDate >= yearStart))
            {
                db.LeaveEntitlements.Add(LeaveEntitlement.Create(tenantId, employee.Id, year, days, 0));
                entitlements++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportEmployeesResponse(request.Rows.Count, added, existing, entitlements, problems);
    }

    // The domain speaks English; HR reads Polish.
    private static string Describe(DomainException exception) => exception.Message switch
    {
        var m when m.StartsWith("Personnel number", StringComparison.Ordinal) => "Numer ewidencyjny jest pusty, za długi albo zawiera spacje.",
        var m when m.StartsWith("First name", StringComparison.Ordinal) => "Brak imienia albo imię jest za długie.",
        var m when m.StartsWith("Last name", StringComparison.Ordinal) => "Brak nazwiska albo nazwisko jest za długie.",
        var m when m.StartsWith("Phone", StringComparison.Ordinal) => $"Numer telefonu może mieć najwyżej {Employee.PhoneMaxLength} znaków.",
        var m when m.StartsWith("Termination date", StringComparison.Ordinal) => "Data zwolnienia jest wcześniejsza niż data zatrudnienia.",
        _ => exception.Message,
    };
}
