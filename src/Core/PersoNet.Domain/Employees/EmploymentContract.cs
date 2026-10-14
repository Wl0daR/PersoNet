using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Employees;

/// <summary>Kinds of contracts used in Poland. The first three are employment contracts under the Labour Code.</summary>
public enum ContractType
{
    /// <summary>Umowa o pracę na okres próbny (at most 3 months, art. 25 § 2 KP).</summary>
    Probation = 1,

    /// <summary>Umowa o pracę na czas określony.</summary>
    FixedTerm = 2,

    /// <summary>Umowa o pracę na czas nieokreślony.</summary>
    Indefinite = 3,

    /// <summary>Umowa zlecenie (civil law).</summary>
    Mandate = 4,

    /// <summary>Umowa o dzieło (civil law).</summary>
    SpecificWork = 5,

    /// <summary>Współpraca B2B (business-to-business).</summary>
    B2B = 6,
}

/// <summary>
/// A contract between the company and an employee: its kind, period and working time. Employment contracts of one employee
/// must not overlap (checked by the caller, which sees all of them); civil contracts may run alongside.
/// </summary>
public sealed class EmploymentContract : Entity, ITenantOwned
{
    public const int MaxProbationMonths = 3;

    // Used by EF Core.
    private EmploymentContract()
    {
    }

    private EmploymentContract(Guid tenantId, Guid employeeId)
    {
        TenantId = tenantId;
        EmployeeId = employeeId;
    }

    public Guid TenantId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public ContractType Type { get; private set; }

    public DateOnly StartDate { get; private set; }

    /// <summary>Last day of the contract; null only for an indefinite contract (or a civil one without an end).</summary>
    public DateOnly? EndDate { get; private set; }

    /// <summary>
    /// The day a contract ended before its planned end, because the employment ended (rozwiązanie umowy); null while it
    /// runs to <see cref="EndDate"/>.
    /// </summary>
    public DateOnly? TerminatedOn { get; private set; }

    /// <summary>The day the contract actually ends: the earlier of <see cref="EndDate"/> and <see cref="TerminatedOn"/>.</summary>
    public DateOnly? LastDay => TerminatedOn is { } terminated && (EndDate is null || terminated < EndDate) ? terminated : EndDate;

    /// <summary>Working time as a fraction of full time ("wymiar etatu"), e.g. 1 or 0.5. Only for employment contracts.</summary>
    public decimal? WorkingTime { get; private set; }

    public bool IsEmployment => IsEmploymentType(Type);

    public static bool IsEmploymentType(ContractType type) =>
        type is ContractType.Probation or ContractType.FixedTerm or ContractType.Indefinite;

    public static EmploymentContract Create(Guid tenantId, Guid employeeId, ContractTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (tenantId == Guid.Empty || employeeId == Guid.Empty)
        {
            throw new DomainException("Contract must belong to a tenant and an employee.");
        }

        var contract = new EmploymentContract(tenantId, employeeId);
        contract.Change(terms);
        return contract;
    }

    public void Change(ContractTerms terms)
    {
        ArgumentNullException.ThrowIfNull(terms);

        if (!Enum.IsDefined(terms.Type))
        {
            throw new DomainException("Unknown contract type.");
        }

        if (terms.EndDate is { } end && end < terms.StartDate)
        {
            throw new DomainException("Contract cannot end before it starts.");
        }

        switch (terms.Type)
        {
            case ContractType.Indefinite when terms.EndDate is not null:
                throw new DomainException("An indefinite contract has no end date.");
            case ContractType.Probation or ContractType.FixedTerm when terms.EndDate is null:
                throw new DomainException("A probation or fixed-term contract needs an end date.");
            case ContractType.Probation when terms.EndDate > terms.StartDate.AddMonths(MaxProbationMonths).AddDays(-1):
                throw new DomainException("A probation contract can last at most 3 months.");
        }

        var employment = IsEmploymentType(terms.Type);

        if (employment && terms.WorkingTime is not (> 0m and <= 1m))
        {
            throw new DomainException("Working time must be more than 0 and at most 1 (full time).");
        }

        Type = terms.Type;
        StartDate = terms.StartDate;
        EndDate = terms.EndDate;
        WorkingTime = employment ? terms.WorkingTime : null;
    }

    /// <summary>
    /// Ends the contract on <paramref name="lastDay"/> when the employment ends. A contract that ends by then anyway is left
    /// as it is.
    /// </summary>
    public void EndEarly(DateOnly lastDay)
    {
        if (lastDay < StartDate)
        {
            throw new DomainException("A contract cannot end before it starts.");
        }

        if (LastDay is { } last && last <= lastDay)
        {
            return;
        }

        TerminatedOn = lastDay;
    }

    /// <summary>Lets the contract run to its planned end again (the end of employment was undone).</summary>
    public void CancelEarlyEnd() => TerminatedOn = null;

    /// <summary>True when the contract covers at least one day of the given period (up to the day it actually ends).</summary>
    public bool Overlaps(DateOnly start, DateOnly? end) =>
        StartDate <= (end ?? DateOnly.MaxValue) && start <= (LastDay ?? DateOnly.MaxValue);
}

public sealed record ContractTerms(ContractType Type, DateOnly StartDate, DateOnly? EndDate, decimal? WorkingTime);
