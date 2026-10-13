using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Employees;

namespace PersoNet.Application.Employees;

/// <summary>Contracts of one employee: list, add, change, remove (for entries made by mistake).</summary>
public sealed class ContractService(IAppDbContext db, ITenantContext tenant)
{
    public async Task<Result<IReadOnlyList<ContractResponse>>> ListAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        if (!await db.Employees.AnyAsync(employee => employee.Id == employeeId, cancellationToken))
        {
            return EmployeeErrors.NotFound;
        }

        var contracts = await db.Contracts.AsNoTracking()
            .Where(contract => contract.EmployeeId == employeeId)
            .OrderByDescending(contract => contract.StartDate)
            .ToListAsync(cancellationToken);

        return contracts.Select(ToResponse).ToList();
    }

    public async Task<Result<ContractResponse>> CreateAsync(Guid employeeId, SaveContractRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (tenant.TenantId is not { } tenantId)
        {
            return EmployeeErrors.NoTenant;
        }

        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
        if (employee is null)
        {
            return EmployeeErrors.NotFound;
        }

        var terms = ToTerms(request);
        if (await CheckAsync(employee, contractId: null, terms, cancellationToken) is { } error)
        {
            return error;
        }

        var contract = EmploymentContract.Create(tenantId, employeeId, terms);
        db.Contracts.Add(contract);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(contract);
    }

    public async Task<Result<ContractResponse>> UpdateAsync(Guid employeeId, Guid contractId, SaveContractRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var contract = await db.Contracts.FirstOrDefaultAsync(contract => contract.Id == contractId && contract.EmployeeId == employeeId, cancellationToken);
        var employee = await db.Employees.AsNoTracking().FirstOrDefaultAsync(employee => employee.Id == employeeId, cancellationToken);
        if (contract is null || employee is null)
        {
            return ContractErrors.NotFound;
        }

        var terms = ToTerms(request);
        if (await CheckAsync(employee, contractId, terms, cancellationToken) is { } error)
        {
            return error;
        }

        contract.Change(terms);
        await db.SaveChangesAsync(cancellationToken);

        return ToResponse(contract);
    }

    public async Task<Result> DeleteAsync(Guid employeeId, Guid contractId, CancellationToken cancellationToken)
    {
        var contract = await db.Contracts.FirstOrDefaultAsync(contract => contract.Id == contractId && contract.EmployeeId == employeeId, cancellationToken);
        if (contract is null)
        {
            return ContractErrors.NotFound;
        }

        db.Contracts.Remove(contract);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }

    internal static ContractResponse ToResponse(EmploymentContract contract) =>
        new(contract.Id, contract.EmployeeId, (ContractKind)contract.Type, contract.StartDate, contract.EndDate, contract.WorkingTime);

    private static ContractTerms ToTerms(SaveContractRequest request) =>
        new((ContractType)request.Type, request.StartDate, request.EndDate, request.WorkingTime);

    // The specific rule is reported with its own code, so the screen can show it next to the right field.
    private async Task<AppError?> CheckAsync(Employee employee, Guid? contractId, ContractTerms terms, CancellationToken cancellationToken)
    {
        if (terms.EndDate is { } end && end < terms.StartDate)
        {
            return ContractErrors.EndBeforeStart;
        }

        switch (terms.Type)
        {
            case ContractType.Indefinite when terms.EndDate is not null:
                return ContractErrors.NoEndForIndefinite;
            case ContractType.Probation or ContractType.FixedTerm when terms.EndDate is null:
                return ContractErrors.EndRequired;
            case ContractType.Probation when terms.EndDate > terms.StartDate.AddMonths(EmploymentContract.MaxProbationMonths).AddDays(-1):
                return ContractErrors.ProbationTooLong;
        }

        var employment = EmploymentContract.IsEmploymentType(terms.Type);

        if (employment && terms.WorkingTime is not (> 0m and <= 1m))
        {
            return ContractErrors.WorkingTimeInvalid;
        }

        if (terms.StartDate < employee.HireDate)
        {
            return ContractErrors.BeforeHire;
        }

        if (employment)
        {
            var others = await db.Contracts.AsNoTracking()
                .Where(other => other.EmployeeId == employee.Id && other.Id != contractId)
                .ToListAsync(cancellationToken);

            if (others.Any(other => other.IsEmployment && other.Overlaps(terms.StartDate, terms.EndDate)))
            {
                return ContractErrors.Overlap;
            }
        }

        try
        {
            // Remaining invariants (e.g. an unknown type) are enforced by the entity itself.
            EmploymentContract.Create(Guid.CreateVersion7(), employee.Id, terms);
        }
        catch (DomainException exception)
        {
            return ContractErrors.Invalid(exception);
        }

        return null;
    }
}
