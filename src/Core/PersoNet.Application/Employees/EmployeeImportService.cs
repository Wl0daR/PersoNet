using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;
using PersoNet.Domain.Organization;

namespace PersoNet.Application.Employees;

/// <summary>
/// Moves data over from an earlier system: departments and positions first, then the employees with their department,
/// position, contracts, end of employment and this year's leave entitlement. Everything is matched by what identified it
/// there (department code, position name, personnel number), so the same files can be imported again without duplicates;
/// a row that breaks a rule is reported and the rest are imported.
/// </summary>
public sealed class EmployeeImportService(IAppDbContext db, ITenantContext tenant, TimeProvider time)
{
    public const int MaxRows = 5000;

    public async Task<Result<ImportOrganizationResponse>> ImportOrganizationAsync(ImportOrganizationRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return EmployeeErrors.NoTenant;
        }

        var departments = request.Departments ?? [];
        var positions = request.Positions ?? [];
        if (departments.Count + positions.Count == 0)
        {
            return EmployeeErrors.ImportEmpty;
        }

        if (departments.Count > MaxRows || positions.Count > MaxRows)
        {
            return EmployeeErrors.ImportTooLarge;
        }

        var problems = new List<string>();
        var byCode = await db.Departments.ToDictionaryAsync(department => department.Code, department => department.Id, StringComparer.Ordinal, cancellationToken);
        int departmentsAdded = 0, departmentsExisting = 0;

        // Parents before children: each round adds the departments whose parent is known by now.
        var pending = departments
            .Select(department => department with { Code = Code(department.Code), ParentCode = department.ParentCode is null ? null : Code(department.ParentCode) })
            .ToList();
        while (pending.Count > 0)
        {
            var ready = pending.Where(department => department.ParentCode is null || byCode.ContainsKey(department.ParentCode)).ToList();
            if (ready.Count == 0)
            {
                problems.AddRange(pending.Select(department => $"Dział {department.Name}: nie ma działu nadrzędnego {department.ParentCode}."));
                break;
            }

            foreach (var department in ready)
            {
                pending.Remove(department);
                if (byCode.ContainsKey(department.Code))
                {
                    departmentsExisting++;
                    continue;
                }

                try
                {
                    var created = Department.Create(tenantId, department.Name, department.Code, department.ParentCode is { } parent ? byCode[parent] : null);
                    db.Departments.Add(created);
                    byCode[created.Code] = created.Id;
                    departmentsAdded++;
                }
                catch (DomainException)
                {
                    problems.Add($"Dział {department.Name} ({department.Code}): nazwa jest pusta albo za długa, a kod może mieć najwyżej {Department.CodeMaxLength} znaków bez spacji.");
                }
            }
        }

        // A position already in the company keeps its own departments and gains the imported ones; the same name may
        // come several times, once per department of the earlier system.
        var known = await db.Positions.ToDictionaryAsync(position => position.NormalizedName, StringComparer.Ordinal, cancellationToken);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int positionsAdded = 0, positionsExisting = 0;
        foreach (var imported in positions.Where(position => !string.IsNullOrWhiteSpace(position?.Name)))
        {
            var name = imported.Name.Trim();
            var departmentIds = new List<Guid>();
            foreach (var code in (imported.DepartmentCodes ?? []).Select(Code).Distinct(StringComparer.Ordinal))
            {
                if (byCode.TryGetValue(code, out var departmentId))
                {
                    departmentIds.Add(departmentId);
                }
                else
                {
                    problems.Add($"Stanowisko {name}: nie ma działu {code}.");
                }
            }

            if (known.TryGetValue(name.ToUpperInvariant(), out var existing))
            {
                existing.AssignDepartments(existing.DepartmentIds.Concat(departmentIds));
                if (seen.Add(existing.NormalizedName))
                {
                    positionsExisting++;
                }

                continue;
            }

            try
            {
                var created = Position.Create(tenantId, name, departmentIds);
                db.Positions.Add(created);
                known[created.NormalizedName] = created;
                seen.Add(created.NormalizedName);
                positionsAdded++;
            }
            catch (DomainException)
            {
                problems.Add($"Stanowisko {name}: nazwa może mieć najwyżej {Position.NameMaxLength} znaków.");
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportOrganizationResponse(departmentsAdded, departmentsExisting, positionsAdded, positionsExisting, problems);
    }

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

        var departments = await db.Departments.AsNoTracking()
            .ToDictionaryAsync(department => department.Code, department => department.Id, StringComparer.Ordinal, cancellationToken);
        var positions = await db.Positions.AsNoTracking()
            .ToDictionaryAsync(position => position.NormalizedName, position => position.Id, StringComparer.Ordinal, cancellationToken);

        // Employees already in PersoNet, which this import brings up to date rather than adds again.
        var numbers = request.Rows.Select(row => row.Number?.Trim().ToUpperInvariant() ?? string.Empty).Distinct().ToList();
        var existing = await db.Employees.Where(employee => numbers.Contains(employee.Number)).ToDictionaryAsync(employee => employee.Number, StringComparer.Ordinal, cancellationToken);
        var existingIds = existing.Values.Select(employee => employee.Id).ToList();
        var withContracts = (await db.Contracts.Where(contract => existingIds.Contains(contract.EmployeeId)).Select(contract => contract.EmployeeId).Distinct().ToListAsync(cancellationToken)).ToHashSet();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var problems = new List<ImportRowProblem>();
        int added = 0, unchanged = 0, updated = 0, entitlements = 0, contracts = 0;

        foreach (var row in request.Rows)
        {
            var number = row.Number?.Trim().ToUpperInvariant() ?? string.Empty;
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

            Guid? departmentId = null;
            if (row.DepartmentCode is { } code && !string.IsNullOrWhiteSpace(code))
            {
                if (departments.TryGetValue(Code(code), out var found))
                {
                    departmentId = found;
                }
                else
                {
                    problems.Add(new ImportRowProblem(row.Line, number, $"Nie ma działu o kodzie {Code(code)}; pracownik zostaje bez działu."));
                }
            }

            Guid? positionId = null;
            if (row.Position is { } positionName && !string.IsNullOrWhiteSpace(positionName))
            {
                if (positions.TryGetValue(positionName.Trim().ToUpperInvariant(), out var found))
                {
                    positionId = found;
                }
                else
                {
                    problems.Add(new ImportRowProblem(row.Line, number, $"Nie ma stanowiska „{positionName.Trim()}”; pracownik zostaje bez stanowiska."));
                }
            }

            if (existing.TryGetValue(number, out var current))
            {
                var changed = false;
                if ((departmentId is not null && departmentId != current.DepartmentId) || (positionId is not null && positionId != current.PositionId))
                {
                    current.Update(new EmployeeDetails(
                        current.Number, current.FirstName, current.LastName, current.Email, current.Phone, current.HireDate,
                        departmentId ?? current.DepartmentId, positionId ?? current.PositionId, current.ManagerId));
                    changed = true;
                }

                if (!withContracts.Contains(current.Id))
                {
                    var count = AddContracts(tenantId, current, row, number, problems);
                    contracts += count;
                    changed |= count > 0;
                }

                if (changed)
                {
                    updated++;
                }
                else
                {
                    unchanged++;
                }

                continue;
            }

            Employee employee;
            try
            {
                employee = Employee.Create(
                    tenantId,
                    new EmployeeDetails(number, row.FirstName, row.LastName, null, row.Phone, row.HireDate, departmentId, positionId, null));

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
            contracts += AddContracts(tenantId, employee, row, number, problems);

            // The entitlement only matters to someone who works for the company at some point this year.
            if (row.AnnualLeaveDays is { } days && employee.HireDate <= yearEnd && (employee.TerminationDate is null || employee.TerminationDate >= yearStart))
            {
                db.LeaveEntitlements.Add(LeaveEntitlement.Create(tenantId, employee.Id, year, days, 0));
                entitlements++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new ImportEmployeesResponse(request.Rows.Count, added, unchanged, entitlements, problems, updated, contracts);
    }

    // Periods of employment become contracts of employment for an indefinite period, ended on the period's last day.
    private int AddContracts(Guid tenantId, Employee employee, ImportEmployeeRow row, string number, List<ImportRowProblem> problems)
    {
        var added = new List<EmploymentContract>();
        foreach (var period in (row.Contracts ?? []).OrderBy(period => period.StartDate))
        {
            if (period.EndDate is { } end && end < period.StartDate)
            {
                problems.Add(new ImportRowProblem(row.Line, number, $"Okres zatrudnienia od {period.StartDate:dd.MM.yyyy} kończy się przed początkiem; pominięto go."));
                continue;
            }

            if (added.Any(other => other.Overlaps(period.StartDate, period.EndDate)))
            {
                problems.Add(new ImportRowProblem(row.Line, number, $"Okres zatrudnienia od {period.StartDate:dd.MM.yyyy} nakłada się na wcześniejszy; pominięto go."));
                continue;
            }

            try
            {
                var contract = EmploymentContract.Create(tenantId, employee.Id, new ContractTerms(ContractType.Indefinite, period.StartDate, null, period.WorkingTime));
                if (period.EndDate is { } lastDay)
                {
                    contract.EndEarly(lastDay);
                }

                added.Add(contract);
            }
            catch (DomainException)
            {
                problems.Add(new ImportRowProblem(row.Line, number, $"Okres zatrudnienia od {period.StartDate:dd.MM.yyyy} ma nieprawidłowy wymiar etatu; pominięto go."));
            }
        }

        db.Contracts.AddRange(added);
        return added.Count;
    }

    private static string Code(string code) => code.Trim().ToUpperInvariant();

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
