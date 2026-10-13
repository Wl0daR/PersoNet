using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Organization;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class OrganizationEndpointTests(PersoNetApiFactory factory)
{
    private static readonly Uri Departments = new("/api/v1/organization/departments", UriKind.Relative);
    private static readonly Uri Positions = new("/api/v1/organization/positions", UriKind.Relative);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hr_creates_department_tree_and_lists_it()
    {
        using var hr = factory.CreateSignedInClient();
        var code = UniqueCode();

        var parent = await CreateDepartmentAsync(hr, new CreateDepartmentRequest("Dział IT", code, ParentId: null));
        var child = await CreateDepartmentAsync(hr, new CreateDepartmentRequest("Zespół aplikacji", code + "-APP", parent.Id));

        var list = await hr.GetFromJsonAsync<List<DepartmentResponse>>(Departments, Cancellation);

        list.ShouldNotBeNull();
        list.ShouldContain(department => department.Id == parent.Id && department.ParentId == null);
        list.ShouldContain(department => department.Id == child.Id && department.ParentId == parent.Id);
    }

    [Fact]
    public async Task Duplicate_department_code_is_a_conflict()
    {
        using var hr = factory.CreateSignedInClient();
        var code = UniqueCode();
        await CreateDepartmentAsync(hr, new CreateDepartmentRequest("Kadry", code, ParentId: null));

        using var response = await hr.PostAsJsonAsync(Departments, new CreateDepartmentRequest("Kadry 2", code.ToLowerInvariant(), null), Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.Content.ReadAsStringAsync(Cancellation)).ShouldContain("department.code_taken");
    }

    [Fact]
    public async Task Moving_department_under_its_own_child_is_rejected()
    {
        using var hr = factory.CreateSignedInClient();
        var code = UniqueCode();
        var parent = await CreateDepartmentAsync(hr, new CreateDepartmentRequest("Sprzedaż", code, null));
        var child = await CreateDepartmentAsync(hr, new CreateDepartmentRequest("Sprzedaż B2B", code + "-B2B", parent.Id));

        using var response = await hr.PutAsJsonAsync(
            new Uri($"{Departments}/{parent.Id}", UriKind.Relative),
            new UpdateDepartmentRequest(parent.Name, parent.Code, child.Id),
            Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(Cancellation)).ShouldContain("department.cycle");
    }

    [Fact]
    public async Task Department_with_active_children_cannot_be_deactivated()
    {
        using var hr = factory.CreateSignedInClient();
        var code = UniqueCode();
        var parent = await CreateDepartmentAsync(hr, new CreateDepartmentRequest("Logistyka", code, null));
        await CreateDepartmentAsync(hr, new CreateDepartmentRequest("Magazyn", code + "-MAG", parent.Id));

        using var response = await hr.PostAsync(new Uri($"{Departments}/{parent.Id}/deactivate", UriKind.Relative), null, Cancellation);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Employee_without_hr_role_can_read_but_not_change_structure()
    {
        using var employee = factory.CreateSignedInClient(roles: "employee");

        using var read = await employee.GetAsync(Departments, Cancellation);
        using var write = await employee.PostAsJsonAsync(Departments, new CreateDepartmentRequest("Dział", UniqueCode(), null), Cancellation);

        read.StatusCode.ShouldBe(HttpStatusCode.OK);
        write.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Another_company_does_not_see_or_change_our_structure()
    {
        using var hr = factory.CreateSignedInClient();
        var ours = await CreateDepartmentAsync(hr, new CreateDepartmentRequest("Finanse", UniqueCode(), null));

        using var other = factory.CreateSignedInClient(await factory.CreateTenantAsync());
        var theirList = await other.GetFromJsonAsync<List<DepartmentResponse>>(Departments, Cancellation);
        using var get = await other.GetAsync(new Uri($"{Departments}/{ours.Id}", UriKind.Relative), Cancellation);
        using var update = await other.PutAsJsonAsync(
            new Uri($"{Departments}/{ours.Id}", UriKind.Relative), new UpdateDepartmentRequest("Przejęte", "X", null), Cancellation);

        theirList.ShouldNotBeNull();
        theirList.ShouldNotContain(department => department.Id == ours.Id);
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        update.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Positions_are_unique_regardless_of_letter_case_and_can_be_deactivated()
    {
        using var hr = factory.CreateSignedInClient();
        var name = "Specjalista ds. kadr " + UniqueCode();

        using var created = await hr.PostAsJsonAsync(Positions, new CreatePositionRequest(name), Cancellation);
        var position = await created.Content.ReadFromJsonAsync<PositionResponse>(Cancellation);
        using var duplicate = await hr.PostAsJsonAsync(Positions, new CreatePositionRequest(name.ToUpperInvariant()), Cancellation);
        using var deactivated = await hr.PostAsync(new Uri($"{Positions}/{position!.Id}/deactivate", UriKind.Relative), null, Cancellation);
        var active = await hr.GetFromJsonAsync<List<PositionResponse>>(Positions, Cancellation);

        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        deactivated.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        active!.ShouldNotContain(item => item.Id == position.Id);
    }

    private static async Task<DepartmentResponse> CreateDepartmentAsync(HttpClient client, CreateDepartmentRequest request)
    {
        using var response = await client.PostAsJsonAsync(Departments, request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<DepartmentResponse>(Cancellation))!;
    }

    // Tests share one database, so codes must not collide between tests.
    private static string UniqueCode() => "T" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
