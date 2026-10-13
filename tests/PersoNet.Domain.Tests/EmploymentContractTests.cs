using System.Globalization;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Employees;

namespace PersoNet.Domain.Tests;

public sealed class EmploymentContractTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly Guid EmployeeId = Guid.CreateVersion7();
    private static readonly DateOnly Start = new(2025, 1, 1);

    [Fact]
    public void Indefinite_full_time_contract()
    {
        var contract = EmploymentContract.Create(TenantId, EmployeeId, new ContractTerms(ContractType.Indefinite, Start, null, 1m));

        contract.IsEmployment.ShouldBeTrue();
        contract.WorkingTime.ShouldBe(1m);
        contract.Overlaps(new DateOnly(2030, 1, 1), null).ShouldBeTrue();
    }

    [Fact]
    public void Civil_contract_drops_working_time()
    {
        var contract = EmploymentContract.Create(TenantId, EmployeeId, new ContractTerms(ContractType.Mandate, Start, null, 0.5m));

        contract.WorkingTime.ShouldBeNull();
    }

    [Fact]
    public void Probation_lasts_at_most_three_months()
    {
        EmploymentContract.Create(TenantId, EmployeeId, new ContractTerms(ContractType.Probation, Start, new DateOnly(2025, 3, 31), 1m));

        Should.Throw<DomainException>(() =>
            EmploymentContract.Create(TenantId, EmployeeId, new ContractTerms(ContractType.Probation, Start, new DateOnly(2025, 4, 1), 1m)));
    }

    [Theory]
    [InlineData(ContractType.Indefinite, "2025-12-31", 1.0)]
    [InlineData(ContractType.FixedTerm, null, 1.0)]
    [InlineData(ContractType.FixedTerm, "2024-12-31", 1.0)]
    [InlineData(ContractType.FixedTerm, "2025-12-31", 0.0)]
    [InlineData(ContractType.FixedTerm, "2025-12-31", 1.5)]
    public void Invalid_terms_are_rejected(ContractType type, string? end, double workingTime)
    {
        var terms = new ContractTerms(type, Start, end is null ? null : DateOnly.Parse(end, CultureInfo.InvariantCulture), (decimal)workingTime);

        Should.Throw<DomainException>(() => EmploymentContract.Create(TenantId, EmployeeId, terms));
    }

    [Fact]
    public void Overlap_counts_shared_days_only()
    {
        var contract = EmploymentContract.Create(TenantId, EmployeeId, new ContractTerms(ContractType.FixedTerm, Start, new DateOnly(2025, 6, 30), 1m));

        contract.Overlaps(new DateOnly(2025, 6, 30), null).ShouldBeTrue();
        contract.Overlaps(new DateOnly(2025, 7, 1), null).ShouldBeFalse();
    }
}
