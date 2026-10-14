using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.Organization;

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
        (second.Added, second.Existing).ShouldBe((0, 2));

        var list = (await hr.GetFromJsonAsync<PagedResponse<EmployeeListItem>>(
            new Uri("/api/v1/employees?includeFormer=true", UriKind.Relative), Cancellation))!;
        list.Items.Select(item => (item.Number, item.LastName, item.TerminationDate)).ShouldBe(
            [("2", "Kowalski", (DateOnly?)null), ("1", "Nowak", new DateOnly(2011, 9, 16))]);

        var lukasz = list.Items.Single(item => item.Number == "2");
        var balance = (await hr.GetFromJsonAsync<LeaveBalanceResponse>(new Uri($"/api/v1/employees/{lukasz.Id}/leave/{year}", UriKind.Relative), Cancellation))!;
        (balance.HasEntitlement, balance.EntitlementDays).ShouldBe((true, 26));
    }

    [Fact]
    public async Task Departments_and_positions_come_first_and_employees_get_them_with_their_contracts()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var organization = new ImportOrganizationRequest(
            [new("D2", "MAGAZYN PVC", "W1"), new("W1", "PVC", null), new("D9", "Sierota", "W99")],
            [new("Magazynier", ["D2"]), new(" magazynier ", ["w1"]), new("Operator")]);

        var structure = await PostAsync<ImportOrganizationResponse>(hr, "/api/v1/organization/import", organization);
        var again = await PostAsync<ImportOrganizationResponse>(hr, "/api/v1/organization/import", organization with
        {
            Positions = [new("Operator", ["D2", "D404"])],
        });

        // The same name twice is one position with both departments; the second run adds a department to "Operator".
        (structure.DepartmentsAdded, structure.PositionsAdded, structure.PositionsExisting).ShouldBe((2, 2, 0));
        structure.Problems.Single().ShouldContain("W99");
        (again.DepartmentsAdded, again.DepartmentsExisting, again.PositionsAdded, again.PositionsExisting).ShouldBe((0, 2, 0, 1));
        again.Problems.ShouldContain("Stanowisko Operator: nie ma działu D404.");
        var departments = (await hr.GetFromJsonAsync<List<DepartmentResponse>>(new Uri("/api/v1/organization/departments", UriKind.Relative), Cancellation))!
            .ToDictionary(department => department.Code, department => department.Id);
        var positions = (await hr.GetFromJsonAsync<List<PositionResponse>>(new Uri("/api/v1/organization/positions", UriKind.Relative), Cancellation))!;
        positions.Single(position => position.Name == "Magazynier").DepartmentIds.ShouldBe([departments["D2"], departments["W1"]], ignoreOrder: true);
        positions.Single(position => position.Name == "Operator").DepartmentIds.ShouldBe([departments["D2"]]);

        // First without the structure, as the first version of the import did; the second run fills it in.
        var bare = new ImportEmployeeRow(1, "7", "Ola", "Lis", null, new DateOnly(2015, 3, 1), null, null);
        await PostAsync<ImportEmployeesResponse>(hr, "/api/v1/employees/import", new ImportEmployeesRequest([bare]));
        var full = bare with
        {
            DepartmentCode = "d2",
            Position = "MAGAZYNIER",
            Contracts = [new(new DateOnly(2015, 3, 1), new DateOnly(2016, 2, 29), 1m), new(new DateOnly(2016, 3, 1), null, 0.5m), new(new DateOnly(2016, 6, 1), null, 1m)],
        };
        var unknown = new ImportEmployeeRow(2, "8", "Jan", "Kos", null, new DateOnly(2020, 1, 1), null, null, "D404", "Pilot");

        var result = await PostAsync<ImportEmployeesResponse>(hr, "/api/v1/employees/import", new ImportEmployeesRequest([full, unknown]));
        var rerun = await PostAsync<ImportEmployeesResponse>(hr, "/api/v1/employees/import", new ImportEmployeesRequest([full, unknown]));

        (result.Added, result.Updated, result.ContractsAdded).ShouldBe((1, 1, 2));
        result.Problems.Select(problem => problem.Message).ShouldBe(
        [
            "Okres zatrudnienia od 01.06.2016 nakłada się na wcześniejszy; pominięto go.",
            "Nie ma działu o kodzie D404; pracownik zostaje bez działu.",
            "Nie ma stanowiska „Pilot”; pracownik zostaje bez stanowiska.",
        ]);
        (rerun.Added, rerun.Updated, rerun.Existing, rerun.ContractsAdded).ShouldBe((0, 0, 2, 0));

        var list = (await hr.GetFromJsonAsync<PagedResponse<EmployeeListItem>>(new Uri("/api/v1/employees", UriKind.Relative), Cancellation))!;
        var ola = list.Items.Single(item => item.Number == "7");
        (ola.DepartmentName, ola.PositionName).ShouldBe(("MAGAZYN PVC", "Magazynier"));
        var contracts = (await hr.GetFromJsonAsync<List<ContractResponse>>(new Uri($"/api/v1/employees/{ola.Id}/contracts", UriKind.Relative), Cancellation))!;
        contracts.OrderBy(contract => contract.StartDate).Select(contract => (contract.Type, contract.StartDate, contract.WorkingTime, contract.TerminatedOn)).ShouldBe(
        [
            (ContractKind.Indefinite, new DateOnly(2015, 3, 1), (decimal?)1m, (DateOnly?)new DateOnly(2016, 2, 29)),
            (ContractKind.Indefinite, new DateOnly(2016, 3, 1), 0.5m, null),
        ]);
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
