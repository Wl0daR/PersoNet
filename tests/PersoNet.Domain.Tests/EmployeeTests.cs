using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Employees;

namespace PersoNet.Domain.Tests;

public sealed class EmployeeTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();
    private static readonly DateOnly HireDate = new(2024, 3, 1);

    private static EmployeeDetails Details(string number = "p001", string? email = " Jan.Nowak@Firma.pl ", Guid? managerId = null) =>
        new(number, " Jan ", " Nowak ", email, null, HireDate, DepartmentId: null, PositionId: null, managerId);

    [Fact]
    public void Create_trims_and_normalizes_fields()
    {
        var employee = Employee.Create(TenantId, Details());

        employee.Number.ShouldBe("P001");
        employee.FullName.ShouldBe("Jan Nowak");
        employee.Email.ShouldBe("jan.nowak@firma.pl");
        employee.SearchText.ShouldBe("NOWAK JAN P001 JAN.NOWAK@FIRMA.PL");
    }

    [Theory]
    [InlineData("", "Jan", "Nowak")]
    [InlineData("P 1", "Jan", "Nowak")]
    [InlineData("P1", " ", "Nowak")]
    [InlineData("P1", "Jan", "")]
    public void Create_rejects_missing_or_invalid_fields(string number, string first, string last)
    {
        var details = new EmployeeDetails(number, first, last, null, null, HireDate, null, null, null);

        Should.Throw<DomainException>(() => Employee.Create(TenantId, details));
    }

    [Fact]
    public void Employee_cannot_be_their_own_manager()
    {
        var employee = Employee.Create(TenantId, Details());

        Should.Throw<DomainException>(() => employee.Update(Details(managerId: employee.Id)));
    }

    [Fact]
    public void Terminate_sets_last_day_and_reinstate_clears_it()
    {
        var employee = Employee.Create(TenantId, Details());

        employee.Terminate(new DateOnly(2025, 6, 30));

        employee.IsEmployedOn(new DateOnly(2025, 6, 30)).ShouldBeTrue();
        employee.IsEmployedOn(new DateOnly(2025, 7, 1)).ShouldBeFalse();

        employee.Reinstate();

        employee.TerminationDate.ShouldBeNull();
    }

    [Fact]
    public void Termination_cannot_be_before_hire_date()
    {
        var employee = Employee.Create(TenantId, Details());

        Should.Throw<DomainException>(() => employee.Terminate(HireDate.AddDays(-1)));
    }
}
