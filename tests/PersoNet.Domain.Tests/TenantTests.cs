using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Tenants;

namespace PersoNet.Domain.Tests;

public sealed class TenantTests
{
    [Fact]
    public void Create_normalizes_name_and_subdomain_and_activates()
    {
        var tenant = Tenant.Create("  Acme sp. z o.o. ", " Acme-PL ");

        tenant.Name.ShouldBe("Acme sp. z o.o.");
        tenant.Subdomain.ShouldBe("acme-pl");
        tenant.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("-acme")]
    [InlineData("acme-")]
    [InlineData("acme.pl")]
    [InlineData("acme_pl")]
    [InlineData("żółw")]
    public void Create_rejects_invalid_subdomain(string subdomain)
    {
        Should.Throw<DomainException>(() => Tenant.Create("Acme", subdomain));
    }

    [Fact]
    public void Create_rejects_subdomain_longer_than_63_characters()
    {
        Should.Throw<DomainException>(() => Tenant.Create("Acme", new string('a', 64)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_rejects_empty_name(string name)
    {
        Should.Throw<DomainException>(() => Tenant.Create(name, "acme"));
    }

    [Fact]
    public void Deactivated_tenant_can_be_activated_again()
    {
        var tenant = Tenant.Create("Acme", "acme");

        tenant.Deactivate();
        tenant.IsActive.ShouldBeFalse();

        tenant.Activate();
        tenant.IsActive.ShouldBeTrue();
    }
}
