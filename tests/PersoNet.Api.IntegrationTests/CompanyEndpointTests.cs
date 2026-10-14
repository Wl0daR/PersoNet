using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PersoNet.Contracts.Company;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.SelfService;
using PersoNet.Contracts.WorkTime;
using PersoNet.Infrastructure.Persistence;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class CompanyEndpointTests(PersoNetApiFactory factory)
{
    private const string Company = "/api/v1/company";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hr_fills_in_the_company_data_and_the_numbers_are_checked()
    {
        // Each test works in its own company, so changed settings never reach other tests.
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);

        var defaults = await hr.GetFromJsonAsync<CompanyResponse>(new Uri(Company, UriKind.Relative), Cancellation);
        using var saved = await hr.PutAsJsonAsync(
            new Uri($"{Company}/details", UriKind.Relative),
            new SaveCompanyDetailsRequest("Acme", "Acme Polska sp. z o.o.", "PL 526-104-38-38", "142130400", "ul. Prosta 1", "00-850", "Warszawa", "Kadry@Acme.test", "22 100 20 30"),
            Cancellation);
        var read = await hr.GetFromJsonAsync<CompanyResponse>(new Uri(Company, UriKind.Relative), Cancellation);
        using var badTaxId = await hr.PutAsJsonAsync(new Uri($"{Company}/details", UriKind.Relative), Details("Acme") with { TaxId = "5261043839" }, Cancellation);
        using var badRegon = await hr.PutAsJsonAsync(new Uri($"{Company}/details", UriKind.Relative), Details("Acme") with { Regon = "142130401" }, Cancellation);
        using var badPostalCode = await hr.PutAsJsonAsync(new Uri($"{Company}/details", UriKind.Relative), Details("Acme") with { PostalCode = "00850" }, Cancellation);
        using var noName = await hr.PutAsJsonAsync(new Uri($"{Company}/details", UriKind.Relative), Details(" "), Cancellation);

        defaults!.Name.ShouldBe("Inna firma");
        defaults.TaxId.ShouldBeNull();
        defaults.PortalLeaveKinds.ShouldBe(Enum.GetValues<LeaveKind>());
        saved.StatusCode.ShouldBe(HttpStatusCode.OK, await saved.Content.ReadAsStringAsync(Cancellation));
        read.ShouldBe(new CompanyResponse(
            "Acme", "Acme Polska sp. z o.o.", "5261043838", "142130400", "ul. Prosta 1", "00-850", "Warszawa", "kadry@acme.test", "22 100 20 30", read!.PortalLeaveKinds, WorkTimeSourceKind.Manual));
        await ShouldFailAsync(badTaxId, "company.tax_id_invalid");
        await ShouldFailAsync(badRegon, "company.regon_invalid");
        await ShouldFailAsync(badPostalCode, "company.postal_code_invalid");
        await ShouldFailAsync(noName, "company.name_required");

        // The registered name follows.
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersoNetDbContext>();
        (await db.Tenants.AsNoTracking().SingleAsync(tenant => tenant.Id == tenantId, Cancellation)).Name.ShouldBe("Acme");
    }

    [Fact]
    public async Task Employees_request_in_the_portal_only_the_kinds_of_leave_the_company_allows_there()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var email = $"ela-{Guid.NewGuid():N}@acme.test";
        using var hired = await hr.PostAsJsonAsync(
            new Uri("/api/v1/employees", UriKind.Relative),
            new SaveEmployeeRequest("E1", "Ela", "Portalowa", email, null, new DateOnly(2024, 1, 1), null, null, null),
            Cancellation);
        hired.StatusCode.ShouldBe(HttpStatusCode.Created, await hired.Content.ReadAsStringAsync(Cancellation));
        using var ela = factory.CreateSignedInClient(tenantId, "employee");
        ela.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);

        using var settings = await hr.PutAsJsonAsync(
            new Uri($"{Company}/leave", UriKind.Relative), new SaveLeaveSettingsRequest([LeaveKind.Occasional, LeaveKind.Annual, LeaveKind.Annual]), Cancellation);
        var company = await ela.GetFromJsonAsync<CompanyResponse>(new Uri(Company, UriKind.Relative), Cancellation);
        using var unpaid = await ela.PostAsJsonAsync(
            new Uri("/api/v1/me/leave-requests", UriKind.Relative),
            new MyLeaveRequest(LeaveKind.Unpaid, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 3), null),
            Cancellation);
        using var occasional = await ela.PostAsJsonAsync(
            new Uri("/api/v1/me/leave-requests", UriKind.Relative),
            new MyLeaveRequest(LeaveKind.Occasional, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 3), "Ślub"),
            Cancellation);
        using var employeeChangesSettings = await ela.PutAsJsonAsync(
            new Uri($"{Company}/leave", UriKind.Relative), new SaveLeaveSettingsRequest([LeaveKind.Unpaid]), Cancellation);
        using var employeeChangesDetails = await ela.PutAsJsonAsync(new Uri($"{Company}/details", UriKind.Relative), Details("Moja firma"), Cancellation);

        settings.StatusCode.ShouldBe(HttpStatusCode.OK, await settings.Content.ReadAsStringAsync(Cancellation));
        company!.PortalLeaveKinds.ShouldBe([LeaveKind.Annual, LeaveKind.Occasional]);
        await ShouldFailAsync(unpaid, "leave.kind_not_in_portal");
        occasional.StatusCode.ShouldBe(HttpStatusCode.Created, await occasional.Content.ReadAsStringAsync(Cancellation));
        employeeChangesSettings.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        employeeChangesDetails.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static SaveCompanyDetailsRequest Details(string name) => new(name, null, null, null, null, null, null, null, null);

    private static async Task ShouldFailAsync(HttpResponseMessage response, string code)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, await response.Content.ReadAsStringAsync(Cancellation));
        (await response.Content.ReadAsStringAsync(Cancellation)).ShouldContain(code);
    }
}
