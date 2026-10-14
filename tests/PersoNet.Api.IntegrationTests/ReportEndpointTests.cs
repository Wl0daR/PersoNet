using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.Organization;
using PersoNet.Contracts.Reports;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ReportEndpointTests(PersoNetApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Headcount_counts_people_employed_on_the_day_and_their_full_time_equivalent_per_department()
    {
        // A company of its own, so the report holds only the people of this test.
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var sales = await CreateDepartmentAsync(hr, "Sprzedaż", "SPR");
        var ala = await HireAsync(hr, "Ala", "Nowak", sales.Id, new DateOnly(2024, 1, 1));
        var bartek = await HireAsync(hr, "Bartek", "Adamski", sales.Id, new DateOnly(2024, 1, 1));
        var celina = await HireAsync(hr, "Celina", "Zięba", null, new DateOnly(2024, 1, 1));
        await HireAsync(hr, "Daria", "Później", sales.Id, new DateOnly(2025, 7, 1));
        await AddContractAsync(hr, ala.Id, new SaveContractRequest(ContractKind.Indefinite, new DateOnly(2024, 1, 1), null, 1m));
        await AddContractAsync(hr, bartek.Id, new SaveContractRequest(ContractKind.Mandate, new DateOnly(2024, 1, 1), null, null));
        await AddContractAsync(hr, bartek.Id, new SaveContractRequest(ContractKind.FixedTerm, new DateOnly(2024, 1, 1), new DateOnly(2025, 12, 31), 0.5m));

        var report = await hr.GetFromJsonAsync<HeadcountReport>(new Uri("/api/v1/reports/headcount?date=2025-06-30", UriKind.Relative), Cancellation);
        using var badDate = await hr.GetAsync(new Uri("/api/v1/reports/headcount?date=1999-12-31", UriKind.Relative), Cancellation);

        // Daria starts after the day. Bartek works under the employment contract, not the civil one beside it.
        report!.People.ShouldBe(3);
        report.FullTimeEquivalent.ShouldBe(1.5m);
        report.Rows.Select(row => row.LastName).ShouldBe(["Adamski", "Nowak", "Zięba"]);
        report.Rows[0].ContractKind.ShouldBe(ContractKind.FixedTerm);
        report.Rows[0].ContractEndDate.ShouldBe(new DateOnly(2025, 12, 31));
        report.Rows[2].ContractKind.ShouldBeNull();
        report.Departments.ShouldBe([new HeadcountDepartment("Sprzedaż", 2, 1.5m), new HeadcountDepartment(null, 1, 0m)]);
        report.Rows.Single(row => row.EmployeeId == celina.Id).DepartmentName.ShouldBeNull();
        badDate.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Leave_usage_shows_the_entitlement_taken_waiting_and_left_of_each_person()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var ala = await HireAsync(hr, "Ala", "Nowak", null, new DateOnly(2024, 1, 1));
        var bartek = await HireAsync(hr, "Bartek", "Adamski", null, new DateOnly(2024, 1, 1));
        await AddContractAsync(hr, ala.Id, new SaveContractRequest(ContractKind.Indefinite, new DateOnly(2024, 1, 1), null, 1m));
        await SetEntitlementAsync(hr, ala.Id, 2025, new SaveLeaveEntitlementRequest(26, 2));

        // Monday to Friday approved (5 days), one day waiting.
        var approved = await CreateLeaveAsync(hr, new CreateLeaveRequest(ala.Id, LeaveKind.Annual, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6), null));
        await DecideAsync(hr, approved.Id, "approve");
        await CreateLeaveAsync(hr, new CreateLeaveRequest(ala.Id, LeaveKind.OnDemand, new DateOnly(2025, 6, 10), new DateOnly(2025, 6, 10), null));

        var report = await hr.GetFromJsonAsync<LeaveUsageReport>(new Uri("/api/v1/reports/leave-usage?year=2025", UriKind.Relative), Cancellation);

        report!.Year.ShouldBe(2025);
        report.Rows.Select(row => row.LastName).ShouldBe(["Adamski", "Nowak"]);
        report.Rows[0].HasEntitlement.ShouldBeFalse();
        report.Rows[0].EmployeeId.ShouldBe(bartek.Id);
        report.Rows[1].ShouldBe(new LeaveUsageRow(ala.Id, ala.Number, "Ala", "Nowak", null, true, 26, 2, 224m, 40m, 8m, 176m, 1, 8m));
    }

    [Fact]
    public async Task Contracts_ending_lists_the_ones_that_end_in_the_period_and_tells_whether_another_follows()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var ala = await HireAsync(hr, "Ala", "Nowak", null, new DateOnly(2025, 1, 1));
        var bartek = await HireAsync(hr, "Bartek", "Adamski", null, new DateOnly(2025, 1, 1));
        var ending = await AddContractAsync(hr, ala.Id, new SaveContractRequest(ContractKind.Probation, new DateOnly(2025, 4, 1), new DateOnly(2025, 6, 30), 1m));
        await AddContractAsync(hr, ala.Id, new SaveContractRequest(ContractKind.Indefinite, new DateOnly(2025, 7, 1), null, 1m));
        var mandate = await AddContractAsync(hr, bartek.Id, new SaveContractRequest(ContractKind.Mandate, new DateOnly(2025, 1, 1), new DateOnly(2025, 6, 15), null));
        await AddContractAsync(hr, bartek.Id, new SaveContractRequest(ContractKind.FixedTerm, new DateOnly(2025, 1, 1), new DateOnly(2025, 9, 30), 1m));

        var report = await hr.GetFromJsonAsync<ContractsEndingReport>(
            new Uri("/api/v1/reports/contracts-ending?from=2025-06-01&days=30", UriKind.Relative), Cancellation);
        using var tooLong = await hr.GetAsync(new Uri("/api/v1/reports/contracts-ending?from=2025-06-01&days=400", UriKind.Relative), Cancellation);

        // The fixed-term contract ends after the period; the mandate is followed by nothing that starts later.
        report!.From.ShouldBe(new DateOnly(2025, 6, 1));
        report.To.ShouldBe(new DateOnly(2025, 6, 30));
        report.Rows.Select(row => row.ContractId).ShouldBe([mandate.Id, ending.Id]);
        report.Rows[0].HasNextContract.ShouldBeFalse();
        report.Rows[1].HasNextContract.ShouldBeTrue();
        report.Rows[1].Kind.ShouldBe(ContractKind.Probation);
        tooLong.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Reports_need_their_own_permission()
    {
        using var employee = factory.CreateSignedInClient(null, "employee");

        using var headcount = await employee.GetAsync(new Uri("/api/v1/reports/headcount", UriKind.Relative), Cancellation);
        using var leave = await employee.GetAsync(new Uri("/api/v1/reports/leave-usage", UriKind.Relative), Cancellation);
        using var contracts = await employee.GetAsync(new Uri("/api/v1/reports/contracts-ending", UriKind.Relative), Cancellation);

        headcount.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        leave.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        contracts.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<DepartmentResponse> CreateDepartmentAsync(HttpClient hr, string name, string code)
    {
        using var response = await hr.PostAsJsonAsync(new Uri("/api/v1/organization/departments", UriKind.Relative), new CreateDepartmentRequest(name, code, null), Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<DepartmentResponse>(Cancellation))!;
    }

    private static async Task<EmployeeResponse> HireAsync(HttpClient hr, string firstName, string lastName, Guid? departmentId, DateOnly hireDate)
    {
        var number = "R" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using var response = await hr.PostAsJsonAsync(
            new Uri("/api/v1/employees", UriKind.Relative),
            new SaveEmployeeRequest(number, firstName, lastName, null, null, hireDate, departmentId, null, null),
            Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation))!;
    }

    private static async Task<ContractResponse> AddContractAsync(HttpClient hr, Guid employeeId, SaveContractRequest request)
    {
        using var response = await hr.PostAsJsonAsync(new Uri($"/api/v1/employees/{employeeId}/contracts", UriKind.Relative), request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<ContractResponse>(Cancellation))!;
    }

    private static async Task SetEntitlementAsync(HttpClient hr, Guid employeeId, int year, SaveLeaveEntitlementRequest request)
    {
        using var response = await hr.PutAsJsonAsync(new Uri($"/api/v1/employees/{employeeId}/leave/{year}", UriKind.Relative), request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancellation));
    }

    private static async Task<LeaveRequestResponse> CreateLeaveAsync(HttpClient hr, CreateLeaveRequest request)
    {
        using var response = await hr.PostAsJsonAsync(new Uri("/api/v1/leave-requests", UriKind.Relative), request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!;
    }

    private static async Task DecideAsync(HttpClient hr, Guid id, string action)
    {
        using var response = await hr.PostAsJsonAsync(new Uri($"/api/v1/leave-requests/{id}/{action}", UriKind.Relative), new LeaveDecisionRequest(null), Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancellation));
    }
}
