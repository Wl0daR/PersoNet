using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Common;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Organization;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class EmployeeEndpointTests(PersoNetApiFactory factory)
{
    private static readonly Uri Employees = new("/api/v1/employees", UriKind.Relative);
    private static readonly DateOnly HireDate = new(2024, 3, 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hr_hires_an_employee_and_finds_them_in_the_list_with_names_resolved()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = UniqueTag();
        var department = await PostAsync<DepartmentResponse>(hr, "/api/v1/organization/departments", new CreateDepartmentRequest("Dział " + tag, tag, null));
        var position = await PostAsync<PositionResponse>(hr, "/api/v1/organization/positions", new CreatePositionRequest("Programista " + tag));
        var manager = await HireAsync(hr, Request(tag + "-M", "Ewa", "Kierownik" + tag));

        var employee = await HireAsync(hr, Request(tag, "Jan", "Nowak" + tag) with
        {
            DepartmentId = department.Id,
            PositionId = position.Id,
            ManagerId = manager.Id,
        });

        var page = await hr.GetFromJsonAsync<PagedResponse<EmployeeListItem>>($"{Employees}?search={("nowak" + tag).ToLowerInvariant()}", Cancellation);

        var row = page!.Items.ShouldHaveSingleItem();
        row.Id.ShouldBe(employee.Id);
        row.DepartmentName.ShouldBe(department.Name);
        row.PositionName.ShouldBe(position.Name);
        row.ManagerName.ShouldBe("Ewa Kierownik" + tag);
        page.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task Duplicate_personnel_number_and_email_are_conflicts()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = UniqueTag();
        await HireAsync(hr, Request(tag, "Anna", "Zielińska") with { Email = $"{tag}@firma.test" });

        using var sameNumber = await hr.PostAsJsonAsync(Employees, Request(tag.ToLowerInvariant(), "Inna", "Osoba"), Cancellation);
        using var sameEmail = await hr.PostAsJsonAsync(Employees, Request(tag + "B", "Inna", "Osoba") with { Email = $"{tag.ToUpperInvariant()}@FIRMA.TEST" }, Cancellation);

        sameNumber.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await sameNumber.Content.ReadAsStringAsync(Cancellation)).ShouldContain("employee.number_taken");
        sameEmail.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await sameEmail.Content.ReadAsStringAsync(Cancellation)).ShouldContain("employee.email_taken");
    }

    [Fact]
    public async Task Manager_cannot_report_to_their_own_subordinate()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = UniqueTag();
        var boss = await HireAsync(hr, Request(tag + "-B", "Szef", "Firmy"));
        var report = await HireAsync(hr, Request(tag + "-R", "Pracownik", "Zespołu") with { ManagerId = boss.Id });

        using var response = await hr.PutAsJsonAsync(
            new Uri($"{Employees}/{boss.Id}", UriKind.Relative),
            Request(tag + "-B", "Szef", "Firmy") with { ManagerId = report.Id },
            Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Cancellation)).ShouldContain("employee.manager_cycle");
    }

    [Fact]
    public async Task Terminated_employee_is_hidden_from_the_list_until_former_employees_are_included()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = UniqueTag();
        var employee = await HireAsync(hr, Request(tag, "Odchodzący", "Pracownik" + tag));

        using var tooEarly = await hr.PostAsJsonAsync(
            new Uri($"{Employees}/{employee.Id}/terminate", UriKind.Relative), new TerminateEmployeeRequest(HireDate.AddDays(-1)), Cancellation);
        using var terminated = await hr.PostAsJsonAsync(
            new Uri($"{Employees}/{employee.Id}/terminate", UriKind.Relative), new TerminateEmployeeRequest(new DateOnly(2025, 1, 31)), Cancellation);

        var current = await hr.GetFromJsonAsync<PagedResponse<EmployeeListItem>>($"{Employees}?search={tag}", Cancellation);
        var all = await hr.GetFromJsonAsync<PagedResponse<EmployeeListItem>>($"{Employees}?search={tag}&includeFormer=true", Cancellation);

        tooEarly.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        terminated.StatusCode.ShouldBe(HttpStatusCode.OK);
        current!.Items.ShouldBeEmpty();
        all!.Items.ShouldHaveSingleItem().TerminationDate.ShouldBe(new DateOnly(2025, 1, 31));
    }

    [Fact]
    public async Task Employees_are_not_visible_to_regular_employees_or_other_companies()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr, Request(UniqueTag(), "Tajny", "Pracownik"));

        using var regular = factory.CreateSignedInClient(roles: "employee");
        using var regularList = await regular.GetAsync(Employees, Cancellation);

        using var other = factory.CreateSignedInClient(await factory.CreateTenantAsync());
        using var otherGet = await other.GetAsync(new Uri($"{Employees}/{employee.Id}", UriKind.Relative), Cancellation);
        var otherList = await other.GetFromJsonAsync<PagedResponse<EmployeeListItem>>(Employees, Cancellation);

        regularList.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        otherGet.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        otherList!.Items.ShouldNotContain(item => item.Id == employee.Id);
    }

    [Fact]
    public async Task Paging_returns_the_requested_slice_and_total()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = UniqueTag();
        for (var i = 1; i <= 3; i++)
        {
            await HireAsync(hr, Request($"{tag}-{i}", "Osoba", $"Strona{tag}{i}"));
        }

        var second = await hr.GetFromJsonAsync<PagedResponse<EmployeeListItem>>($"{Employees}?search=strona{tag}&page=2&pageSize=2", Cancellation);

        second!.TotalCount.ShouldBe(3);
        second.TotalPages.ShouldBe(2);
        second.Items.ShouldHaveSingleItem().Number.ShouldBe($"{tag}-3");
    }

    private static SaveEmployeeRequest Request(string number, string firstName, string lastName) =>
        new(number, firstName, lastName, Email: null, Phone: null, HireDate, DepartmentId: null, PositionId: null, ManagerId: null);

    private static Task<EmployeeResponse> HireAsync(HttpClient client, SaveEmployeeRequest request) =>
        PostAsync<EmployeeResponse>(client, Employees.ToString(), request);

    private static async Task<T> PostAsync<T>(HttpClient client, string uri, object request)
    {
        using var response = await client.PostAsJsonAsync(new Uri(uri, UriKind.Relative), request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<T>(Cancellation))!;
    }

    // Tests share one database, so numbers and names must not collide between tests.
    private static string UniqueTag() => "E" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
