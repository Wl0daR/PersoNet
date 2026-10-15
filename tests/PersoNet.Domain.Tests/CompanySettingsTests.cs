using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Company;
using PersoNet.Domain.Leave;

namespace PersoNet.Domain.Tests;

public sealed class CompanySettingsTests
{
    private static readonly Guid TenantId = Guid.CreateVersion7();

    [Fact]
    public void A_new_company_has_its_name_and_the_usual_kinds_of_leave_in_the_portal()
    {
        var settings = CompanySettings.CreateDefault(TenantId, " Acme ");

        settings.Name.ShouldBe("Acme");
        settings.TaxId.ShouldBeNull();
        settings.PortalLeaveTypes.ShouldBe([LeaveType.Annual, LeaveType.OnDemand, LeaveType.Occasional, LeaveType.Unpaid, LeaveType.ChildCare, LeaveType.ForceMajeure]);
    }

    [Theory]
    [InlineData("5261043838", true)]
    [InlineData("5261043839", false)]
    [InlineData("526104383", false)]
    [InlineData("526104383A", false)]
    public void Tax_id_must_have_a_correct_checksum(string taxId, bool valid) =>
        CompanySettings.IsValidTaxId(taxId).ShouldBe(valid);

    [Theory]
    [InlineData("142130400", true)]
    [InlineData("142130401", false)]
    [InlineData("12345678512347", true)]
    [InlineData("1234567851234", false)]
    public void Regon_has_nine_or_fourteen_digits_with_a_checksum(string regon, bool valid) =>
        CompanySettings.IsValidRegon(regon).ShouldBe(valid);

    [Fact]
    public void Numbers_are_kept_as_digits_and_e_mail_in_lower_case()
    {
        var settings = CompanySettings.CreateDefault(TenantId, "Acme");

        settings.ChangeDetails(new CompanyDetails("Acme", null, "PL 526-104-38-38", "142 130 400", null, "00-850", null, "Kadry@Acme.PL", null));

        settings.TaxId.ShouldBe("5261043838");
        settings.Regon.ShouldBe("142130400");
        settings.Email.ShouldBe("kadry@acme.pl");
    }

    [Fact]
    public void Invalid_data_is_refused()
    {
        var settings = CompanySettings.CreateDefault(TenantId, "Acme");

        Should.Throw<DomainException>(() => settings.ChangeDetails(new CompanyDetails(" ", null, null, null, null, null, null, null, null)));
        Should.Throw<DomainException>(() => settings.ChangeDetails(new CompanyDetails("Acme", null, "1234567890", null, null, null, null, null, null)));
        Should.Throw<DomainException>(() => settings.ChangeDetails(new CompanyDetails("Acme", null, null, null, null, "00850", null, null, null)));
        Should.Throw<DomainException>(() => settings.ChangePortalLeaveTypes([(LeaveType)99]));
    }

    [Fact]
    public void Portal_leave_kinds_are_kept_once_and_in_order()
    {
        var settings = CompanySettings.CreateDefault(TenantId, "Acme");

        settings.ChangePortalLeaveTypes([LeaveType.Unpaid, LeaveType.Annual, LeaveType.Unpaid]);

        settings.PortalLeaveTypes.ShouldBe([LeaveType.Annual, LeaveType.Unpaid]);
        settings.AllowsInPortal(LeaveType.OnDemand).ShouldBeFalse();
    }
}
