using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class EmployeeImportEndpointTests(PersoNetApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Import_creates_cards_with_this_years_leave_and_a_second_run_adds_nothing()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var year = DateTime.Today.Year;
        var request = new ImportEmployeesRequest(
        [
            new ImportEmployeeRow(10, "1", "Jacek", "Nowak", "123-456-789", new DateOnly(2010, 9, 6), new DateOnly(2011, 9, 16), 26),
            new ImportEmployeeRow(11, "2", "Łukasz", "Kowalski", null, new DateOnly(2020, 3, 1), null, 26),
            new ImportEmployeeRow(12, "2", "Marcin", "Laskowski", null, new DateOnly(2021, 1, 1), null, 20),
            new ImportEmployeeRow(13, "4", "Ewa", "Zielińska", null, new DateOnly(2022, 5, 10), new DateOnly(2022, 5, 1), 20),
            new ImportEmployeeRow(14, "5", "Anna", "Wiśniewska", null, new DateOnly(2023, 1, 1), null, 99),
        ]);

        var first = await PostAsync<ImportEmployeesResponse>(hr, "/api/v1/employees/import", request);
        var second = await PostAsync<ImportEmployeesResponse>(hr, "/api/v1/employees/import", request);

        (first.Received, first.Added, first.Existing, first.EntitlementsAdded).ShouldBe((5, 2, 0, 1));
        first.Problems.Select(problem => problem.Line).ShouldBe([12, 13, 14]);
        first.Problems[0].Message.ShouldContain("więcej niż raz");
        first.Problems[1].Message.ShouldContain("wcześniejsza niż data zatrudnienia");
        first.Problems[2].Message.ShouldContain("od 0 do 60");
        (second.Added, second.Existing).ShouldBe((0, 3));

        var list = (await hr.GetFromJsonAsync<PagedResponse<EmployeeListItem>>(
            new Uri("/api/v1/employees?includeFormer=true", UriKind.Relative), Cancellation))!;
        list.Items.Select(item => (item.Number, item.LastName, item.TerminationDate)).ShouldBe(
            [("2", "Kowalski", (DateOnly?)null), ("1", "Nowak", new DateOnly(2011, 9, 16))]);

        var lukasz = list.Items.Single(item => item.Number == "2");
        var balance = (await hr.GetFromJsonAsync<LeaveBalanceResponse>(new Uri($"/api/v1/employees/{lukasz.Id}/leave/{year}", UriKind.Relative), Cancellation))!;
        (balance.HasEntitlement, balance.EntitlementDays).ShouldBe((true, 26));
    }

    [Fact]
    public async Task An_empty_import_is_refused()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);

        using var response = await hr.PostAsJsonAsync(new Uri("/api/v1/employees/import", UriKind.Relative), new ImportEmployeesRequest([]), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Cancellation)).ShouldContain("employee.import_empty");
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string uri, object body)
    {
        using var response = await client.PostAsJsonAsync(new Uri(uri, UriKind.Relative), body, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<T>(Cancellation))!;
    }
}
