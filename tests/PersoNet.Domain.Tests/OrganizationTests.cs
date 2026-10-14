using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Organization;

namespace PersoNet.Domain.Tests;

public sealed class OrganizationTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void Department_create_trims_name_and_upper_cases_code()
    {
        var department = Department.Create(TenantId, "  Dział IT ", " it ", parentId: null);

        department.Name.ShouldBe("Dział IT");
        department.Code.ShouldBe("IT");
        department.TenantId.ShouldBe(TenantId);
        department.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", "IT")]
    [InlineData("Dział IT", "")]
    [InlineData("Dział IT", "I T")]
    [InlineData("Dział IT", "ABCDEFGHIJKLMNOPQRSTU")]
    public void Department_create_rejects_invalid_name_or_code(string name, string code)
    {
        Should.Throw<DomainException>(() => Department.Create(TenantId, name, code, parentId: null));
    }

    [Fact]
    public void Department_requires_a_tenant()
    {
        Should.Throw<DomainException>(() => Department.Create(Guid.Empty, "Dział IT", "IT", parentId: null));
    }

    [Fact]
    public void Department_cannot_be_its_own_parent()
    {
        var department = Department.Create(TenantId, "Dział IT", "IT", parentId: null);

        Should.Throw<DomainException>(() => department.MoveTo(department.Id));
    }

    [Fact]
    public void Position_rename_and_deactivate()
    {
        var position = Position.Create(TenantId, "Programista");

        position.Rename(" Starszy programista ");
        position.Deactivate();

        position.Name.ShouldBe("Starszy programista");
        position.IsActive.ShouldBeFalse();
    }

    [Fact]
    public void Position_rejects_empty_name()
    {
        Should.Throw<DomainException>(() => Position.Create(TenantId, "  "));
    }

    [Fact]
    public void Position_departments_have_no_repeats_and_no_empty_id()
    {
        var production = Guid.NewGuid();
        var store = Guid.NewGuid();
        var position = Position.Create(TenantId, "Brygadzista", [production, production]);

        position.DepartmentIds.ShouldBe([production]);
        position.AssignDepartments([store, production]);
        position.DepartmentIds.ShouldBe([store, production]);
        Should.Throw<DomainException>(() => position.AssignDepartments([Guid.Empty]));
        position.AssignDepartments([]);
        position.DepartmentIds.ShouldBeEmpty();
    }
}
