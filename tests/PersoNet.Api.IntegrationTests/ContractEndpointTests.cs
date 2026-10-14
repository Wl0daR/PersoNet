using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Employees;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class ContractEndpointTests(PersoNetApiFactory factory)
{
    private static readonly DateOnly HireDate = new(2024, 3, 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Probation_then_indefinite_contract_are_listed_newest_first_with_type_as_text()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);

        await AddAsync(hr, employee.Id, new SaveContractRequest(ContractKind.Probation, HireDate, new DateOnly(2024, 5, 31), 1m));
        await AddAsync(hr, employee.Id, new SaveContractRequest(ContractKind.Indefinite, new DateOnly(2024, 6, 1), null, 0.5m));

        var json = await hr.GetStringAsync(Contracts(employee.Id), Cancellation);
        var list = await hr.GetFromJsonAsync<List<ContractResponse>>(Contracts(employee.Id), Cancellation);

        list.ShouldNotBeNull();
        json.ShouldContain("\"Indefinite\"");
        list.Select(contract => contract.Type).ShouldBe([ContractKind.Indefinite, ContractKind.Probation]);
        list[0].WorkingTime.ShouldBe(0.5m);
    }

    [Fact]
    public async Task Overlapping_employment_contracts_are_rejected_but_a_civil_one_alongside_is_fine()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);
        await AddAsync(hr, employee.Id, new SaveContractRequest(ContractKind.Indefinite, HireDate, null, 1m));

        using var overlap = await hr.PostAsJsonAsync(
            Contracts(employee.Id), new SaveContractRequest(ContractKind.FixedTerm, new DateOnly(2025, 1, 1), new DateOnly(2025, 12, 31), 1m), Cancellation);
        using var mandate = await hr.PostAsJsonAsync(
            Contracts(employee.Id), new SaveContractRequest(ContractKind.Mandate, new DateOnly(2025, 1, 1), null, null), Cancellation);

        overlap.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await overlap.Content.ReadAsStringAsync(Cancellation)).ShouldContain("contract.overlap");
        mandate.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Theory]
    [InlineData(ContractKind.Probation, "2024-03-01", "2024-06-30", 1.0, "contract.probation_too_long")]
    [InlineData(ContractKind.FixedTerm, "2024-03-01", null, 1.0, "contract.end_required")]
    [InlineData(ContractKind.Indefinite, "2024-03-01", "2025-01-01", 1.0, "contract.no_end_for_indefinite")]
    [InlineData(ContractKind.Indefinite, "2024-03-01", null, 1.5, "contract.working_time_invalid")]
    [InlineData(ContractKind.Indefinite, "2024-02-01", null, 1.0, "contract.before_hire")]
    public async Task Labour_code_rules_are_enforced(ContractKind type, string start, string? end, double workingTime, string code)
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);

        using var response = await hr.PostAsJsonAsync(
            Contracts(employee.Id),
            new SaveContractRequest(type, DateOnly.Parse(start, CultureInfo.InvariantCulture), end is null ? null : DateOnly.Parse(end, CultureInfo.InvariantCulture), (decimal)workingTime),
            Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Cancellation)).ShouldContain(code);
    }

    [Fact]
    public async Task Contract_can_be_changed_and_removed_but_not_by_another_company()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);
        var contract = await AddAsync(hr, employee.Id, new SaveContractRequest(ContractKind.FixedTerm, HireDate, new DateOnly(2024, 12, 31), 1m));

        using var other = factory.CreateSignedInClient(await factory.CreateTenantAsync());
        using var foreignDelete = await other.DeleteAsync(new Uri($"{Contracts(employee.Id)}/{contract.Id}", UriKind.Relative), Cancellation);

        using var update = await hr.PutAsJsonAsync(
            new Uri($"{Contracts(employee.Id)}/{contract.Id}", UriKind.Relative),
            new SaveContractRequest(ContractKind.FixedTerm, HireDate, new DateOnly(2025, 2, 28), 0.75m),
            Cancellation);
        var updated = await update.Content.ReadFromJsonAsync<ContractResponse>(Cancellation);
        using var delete = await hr.DeleteAsync(new Uri($"{Contracts(employee.Id)}/{contract.Id}", UriKind.Relative), Cancellation);
        var remaining = await hr.GetFromJsonAsync<List<ContractResponse>>(Contracts(employee.Id), Cancellation);

        foreignDelete.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        updated!.EndDate.ShouldBe(new DateOnly(2025, 2, 28));
        updated.WorkingTime.ShouldBe(0.75m);
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        remaining!.ShouldBeEmpty();
    }

    [Fact]
    public async Task Ending_the_employment_ends_its_contracts_and_a_rehired_employee_gets_a_new_one()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);
        await AddAsync(hr, employee.Id, new SaveContractRequest(ContractKind.Indefinite, HireDate, null, 1m));
        var lastDay = new DateOnly(2025, 6, 30);

        using var terminate = await hr.PostAsJsonAsync($"/api/v1/employees/{employee.Id}/terminate", new TerminateEmployeeRequest(lastDay), Cancellation);
        var ended = (await hr.GetFromJsonAsync<List<ContractResponse>>(Contracts(employee.Id), Cancellation))!.Single();

        using var tooEarly = await hr.PostAsJsonAsync($"/api/v1/employees/{employee.Id}/rehire", new RehireEmployeeRequest(lastDay), Cancellation);
        using var rehire = await hr.PostAsJsonAsync($"/api/v1/employees/{employee.Id}/rehire", new RehireEmployeeRequest(new DateOnly(2025, 8, 1)), Cancellation);
        var rehired = await rehire.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation);
        var next = await AddAsync(hr, employee.Id, new SaveContractRequest(ContractKind.FixedTerm, new DateOnly(2025, 8, 1), new DateOnly(2026, 7, 31), 1m));

        terminate.StatusCode.ShouldBe(HttpStatusCode.OK);
        ended.EndDate.ShouldBeNull();
        ended.TerminatedOn.ShouldBe(lastDay);
        tooEarly.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await tooEarly.Content.ReadAsStringAsync(Cancellation)).ShouldContain("employee.rehire_before_termination");
        rehired!.HireDate.ShouldBe(new DateOnly(2025, 8, 1));
        rehired.TerminationDate.ShouldBeNull();
        next.StartDate.ShouldBe(new DateOnly(2025, 8, 1));
    }

    [Fact]
    public async Task Undoing_the_end_of_employment_restores_the_contracts_unless_a_new_one_took_their_place()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);
        await AddAsync(hr, employee.Id, new SaveContractRequest(ContractKind.Indefinite, HireDate, null, 1m));
        var lastDay = new DateOnly(2025, 6, 30);
        using var _ = await hr.PostAsJsonAsync($"/api/v1/employees/{employee.Id}/terminate", new TerminateEmployeeRequest(lastDay), Cancellation);

        using var reinstate = await hr.PostAsync(new Uri($"/api/v1/employees/{employee.Id}/reinstate", UriKind.Relative), null, Cancellation);
        var restored = (await hr.GetFromJsonAsync<List<ContractResponse>>(Contracts(employee.Id), Cancellation))!.Single();

        using var again = await hr.PostAsJsonAsync($"/api/v1/employees/{employee.Id}/terminate", new TerminateEmployeeRequest(lastDay), Cancellation);
        await AddAsync(hr, employee.Id, new SaveContractRequest(ContractKind.FixedTerm, new DateOnly(2025, 9, 1), new DateOnly(2025, 12, 31), 1m));
        using var blocked = await hr.PostAsync(new Uri($"/api/v1/employees/{employee.Id}/reinstate", UriKind.Relative), null, Cancellation);

        reinstate.StatusCode.ShouldBe(HttpStatusCode.OK);
        restored.TerminatedOn.ShouldBeNull();
        blocked.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await blocked.Content.ReadAsStringAsync(Cancellation)).ShouldContain("employee.reinstate_overlap");
    }

    private static string Contracts(Guid employeeId) => $"/api/v1/employees/{employeeId}/contracts";

    private static async Task<EmployeeResponse> HireAsync(HttpClient client)
    {
        var number = "C" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/employees", UriKind.Relative),
            new SaveEmployeeRequest(number, "Jan", "Umowny", null, null, HireDate, null, null, null),
            Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation))!;
    }

    private static async Task<ContractResponse> AddAsync(HttpClient client, Guid employeeId, SaveContractRequest request)
    {
        using var response = await client.PostAsJsonAsync(new Uri(Contracts(employeeId), UriKind.Relative), request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<ContractResponse>(Cancellation))!;
    }
}
