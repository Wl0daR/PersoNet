using System.Net;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Company;
using PersoNet.Contracts.Company;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.WorkTime;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>In-memory stand-in for the company settings API; a NIP ending in 9 fails the server's checksum.</summary>
internal sealed class FakeCompanyApi : ICompanyApi
{
    public CompanyResponse Company { get; private set; } =
        new("Acme", null, null, null, null, null, null, null, null, Enum.GetValues<LeaveKind>(), WorkTimeSourceKind.Manual);

    public Task<CompanyResponse> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Company);

    public Task<CompanyResponse> SaveDetailsAsync(SaveCompanyDetailsRequest request, CancellationToken cancellationToken)
    {
        var taxId = string.IsNullOrWhiteSpace(request.TaxId) ? null : string.Concat(request.TaxId.Where(char.IsAsciiDigit));
        if (taxId?.EndsWith('9') == true)
        {
            throw new ApiException(HttpStatusCode.BadRequest, "NIP jest niepoprawny.", "company.tax_id_invalid");
        }

        Company = Company with
        {
            Name = request.Name.Trim(),
            LegalName = Blank(request.LegalName),
            TaxId = taxId,
            Regon = Blank(request.Regon),
            Street = Blank(request.Street),
            PostalCode = Blank(request.PostalCode),
            City = Blank(request.City),
            Email = Blank(request.Email),
            Phone = Blank(request.Phone),
        };
        return Task.FromResult(Company);
    }

    public Task<CompanyResponse> SaveLeaveSettingsAsync(SaveLeaveSettingsRequest request, CancellationToken cancellationToken)
    {
        Company = Company with { PortalLeaveKinds = [.. request.PortalLeaveKinds.Distinct().Order()] };
        return Task.FromResult(Company);
    }

    public Task<CompanyResponse> SaveWorkTimeSettingsAsync(SaveWorkTimeSettingsRequest request, CancellationToken cancellationToken)
    {
        Company = Company with { WorkTimeSource = request.Source };
        return Task.FromResult(Company);
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
