using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Access;
using PersoNet.Contracts.Account;
using PersoNet.Contracts.Audit;
using PersoNet.Contracts.Employees;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class AccessEndpointTests(PersoNetApiFactory factory)
{
    private const string Roles = "/api/v1/roles";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Role_given_to_an_employee_lets_them_do_exactly_what_it_permits_until_it_is_taken_away()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = Tag();
        var email = $"kadrowa-{tag}@acme.test";
        var employee = await HireAsync(hr, "K" + tag, email);
        var role = await CreateRoleAsync(hr, new SaveRoleRequest($"Kadrowa {tag}", "Przegląda kartoteki", ["leave.read", "employees.read"]));
        using var person = factory.CreatePortalClient(email);

        using var before = await person.GetAsync(new Uri("/api/v1/employees", UriKind.Relative), Cancellation);
        await SetRolesAsync(hr, employee.Id, role.Id);
        using var list = await person.GetAsync(new Uri("/api/v1/employees", UriKind.Relative), Cancellation);
        using var hire = await person.PostAsJsonAsync(new Uri("/api/v1/employees", UriKind.Relative), Request("X" + tag, null), Cancellation);
        var me = await person.GetFromJsonAsync<CurrentUserResponse>(new Uri("/api/v1/me", UriKind.Relative), Cancellation);
        await SetRolesAsync(hr, employee.Id);
        using var after = await person.GetAsync(new Uri("/api/v1/employees", UriKind.Relative), Cancellation);

        before.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        hire.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        me!.Permissions.ShouldBe(["employees.read", "leave.read"]);
        after.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        role.Permissions.ShouldBe(["employees.read", "leave.read"]);

        var history = await hr.GetFromJsonAsync<List<HistoryEntryResponse>>(new Uri($"/api/v1/employees/{employee.Id}/history", UriKind.Relative), Cancellation);
        history!.Select(entry => entry.Description).Take(2).ShouldBe([$"Odebrano rolę: Kadrowa {tag}", $"Nadano rolę: Kadrowa {tag}"]);
    }

    [Fact]
    public async Task Administrator_sees_every_permission_and_roles_cannot_grant_account_management()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = Tag();

        var me = await hr.GetFromJsonAsync<CurrentUserResponse>(new Uri("/api/v1/me", UriKind.Relative), Cancellation);
        var permissions = await hr.GetFromJsonAsync<List<PermissionResponse>>(new Uri("/api/v1/permissions", UriKind.Relative), Cancellation);
        using var accounts = await hr.PostAsJsonAsync(new Uri(Roles, UriKind.Relative), new SaveRoleRequest($"Konta {tag}", null, ["accounts.manage"]), Cancellation);
        using var unknown = await hr.PostAsJsonAsync(new Uri(Roles, UriKind.Relative), new SaveRoleRequest($"Nieznane {tag}", null, ["payroll.read"]), Cancellation);
        using var noName = await hr.PostAsJsonAsync(new Uri(Roles, UriKind.Relative), new SaveRoleRequest(" ", null, []), Cancellation);

        me!.Permissions.ShouldContain("roles.manage");
        me.Permissions.ShouldContain("accounts.manage");
        permissions!.Single(permission => permission.Code == "accounts.manage").Assignable.ShouldBeFalse();
        permissions!.Single(permission => permission.Code == "employees.read").Name.ShouldBe("Przeglądanie kartotek");
        await ShouldFailAsync(accounts, HttpStatusCode.BadRequest, "role.permission_unknown");
        await ShouldFailAsync(unknown, HttpStatusCode.BadRequest, "role.permission_unknown");
        await ShouldFailAsync(noName, HttpStatusCode.BadRequest, "role.name_required");
    }

    [Fact]
    public async Task Names_are_unique_and_a_role_people_have_cannot_be_removed()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = Tag();
        var employee = await HireAsync(hr, "R" + tag, null);
        var role = await CreateRoleAsync(hr, new SaveRoleRequest($"Kierownik {tag}", null, ["leave.read"]));

        using var duplicate = await hr.PostAsJsonAsync(new Uri(Roles, UriKind.Relative), new SaveRoleRequest($"KIEROWNIK {tag}", null, []), Cancellation);
        await SetRolesAsync(hr, employee.Id, role.Id);
        var listed = await hr.GetFromJsonAsync<List<RoleResponse>>(new Uri(Roles, UriKind.Relative), Cancellation);
        using var inUse = await hr.DeleteAsync(new Uri($"{Roles}/{role.Id}", UriKind.Relative), Cancellation);
        using var renamed = await hr.PutAsJsonAsync(
            new Uri($"{Roles}/{role.Id}", UriKind.Relative), new SaveRoleRequest($"Kierownik działu {tag}", "Urlopy zespołu", ["leave.read", "leave.manage"]), Cancellation);
        await SetRolesAsync(hr, employee.Id);
        using var deleted = await hr.DeleteAsync(new Uri($"{Roles}/{role.Id}", UriKind.Relative), Cancellation);
        using var otherCompany = factory.CreateSignedInClient(await factory.CreateTenantAsync());
        var foreign = await otherCompany.GetFromJsonAsync<List<RoleResponse>>(new Uri(Roles, UriKind.Relative), Cancellation);

        await ShouldFailAsync(duplicate, HttpStatusCode.Conflict, "role.name_taken");
        listed!.Single(item => item.Id == role.Id).MemberCount.ShouldBe(1);
        await ShouldFailAsync(inUse, HttpStatusCode.Conflict, "role.in_use");
        (await renamed.Content.ReadFromJsonAsync<RoleResponse>(Cancellation))!.Permissions.ShouldBe(["leave.manage", "leave.read"]);
        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        foreign!.ShouldBeEmpty();
    }

    private static string Tag() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

    private static SaveEmployeeRequest Request(string number, string? email) =>
        new(number, "Ewa", "Uprawniona", email, null, new DateOnly(2024, 1, 1), null, null, null);

    private static async Task<EmployeeResponse> HireAsync(HttpClient hr, string number, string? email)
    {
        using var response = await hr.PostAsJsonAsync(new Uri("/api/v1/employees", UriKind.Relative), Request(number, email), Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation))!;
    }

    private static async Task<RoleResponse> CreateRoleAsync(HttpClient hr, SaveRoleRequest request)
    {
        using var response = await hr.PostAsJsonAsync(new Uri(Roles, UriKind.Relative), request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<RoleResponse>(Cancellation))!;
    }

    private static async Task SetRolesAsync(HttpClient hr, Guid employeeId, params Guid[] roleIds)
    {
        using var response = await hr.PutAsJsonAsync(
            new Uri($"/api/v1/employees/{employeeId}/roles", UriKind.Relative), new SetEmployeeRolesRequest(roleIds), Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancellation));
    }

    private static async Task ShouldFailAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        response.StatusCode.ShouldBe(status, await response.Content.ReadAsStringAsync(Cancellation));
        (await response.Content.ReadAsStringAsync(Cancellation)).ShouldContain(code);
    }
}
