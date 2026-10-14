using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.SelfService;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class MeEndpointTests(PersoNetApiFactory factory)
{
    private static readonly DateOnly HireDate = new(2024, 3, 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Account_without_an_employee_record_is_told_to_ask_hr()
    {
        using var stranger = factory.CreatePortalClient($"nikt-{Guid.NewGuid():N}@acme.test");

        using var response = await stranger.GetAsync(new Uri("/api/v1/me/employee", UriKind.Relative), Cancellation);

        await ShouldFailAsync(response, HttpStatusCode.Forbidden, "self.not_linked");
    }

    [Fact]
    public async Task Employee_files_their_own_request_sees_it_with_the_balance_and_can_withdraw_it_while_pending()
    {
        using var hr = factory.CreateSignedInClient();
        var email = Email("jan");
        var employee = await HireAsync(hr, "Jan", "Portalowy", email.ToUpperInvariant(), managerId: null);
        await SetEntitlementAsync(hr, employee.Id, 20);
        using var jan = factory.CreatePortalClient(email);

        var profile = await jan.GetFromJsonAsync<MyProfileResponse>(new Uri("/api/v1/me/employee", UriKind.Relative), Cancellation);
        using var created = await jan.PostAsJsonAsync(
            new Uri("/api/v1/me/leave-requests", UriKind.Relative),
            new MyLeaveRequest(LeaveKind.Annual, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6), "Wakacje"),
            Cancellation);
        var request = (await created.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!;
        var balance = await jan.GetFromJsonAsync<LeaveBalanceResponse>(new Uri("/api/v1/me/leave/2025", UriKind.Relative), Cancellation);
        var mine = await jan.GetFromJsonAsync<List<LeaveRequestResponse>>(new Uri("/api/v1/me/leave-requests?year=2025", UriKind.Relative), Cancellation);
        using var withdrawn = await jan.PostAsync(new Uri($"/api/v1/me/leave-requests/{request.Id}/withdraw", UriKind.Relative), null, Cancellation);
        using var again = await jan.PostAsync(new Uri($"/api/v1/me/leave-requests/{request.Id}/withdraw", UriKind.Relative), null, Cancellation);

        profile!.EmployeeId.ShouldBe(employee.Id);
        profile.TeamSize.ShouldBe(0);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        request.EmployeeId.ShouldBe(employee.Id);
        request.State.ShouldBe(LeaveState.Pending);
        balance!.PendingHours.ShouldBe(40m);
        mine.ShouldHaveSingleItem().Id.ShouldBe(request.Id);
        withdrawn.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await withdrawn.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!.State.ShouldBe(LeaveState.Cancelled);
        await ShouldFailAsync(again, HttpStatusCode.BadRequest, "self.not_pending");
    }

    [Fact]
    public async Task Employee_cannot_use_hr_endpoints()
    {
        using var hr = factory.CreateSignedInClient();
        var email = Email("ewa");
        await HireAsync(hr, "Ewa", "Bezuprawnień", email, managerId: null);
        using var ewa = factory.CreatePortalClient(email);

        using var all = await ewa.GetAsync(new Uri("/api/v1/leave-requests", UriKind.Relative), Cancellation);
        using var holidays = await ewa.GetAsync(new Uri("/api/v1/holidays/2025", UriKind.Relative), Cancellation);

        all.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        holidays.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Manager_decides_only_the_requests_of_their_own_team()
    {
        using var hr = factory.CreateSignedInClient();
        var bossEmail = Email("szef");
        var boss = await HireAsync(hr, "Adam", "Kierownik", bossEmail, managerId: null);
        var worker = await HireAsync(hr, "Ola", "Zespołowa", Email("ola"), boss.Id);
        var outsider = await HireAsync(hr, "Piotr", "Obcy", Email("piotr"), managerId: null);
        var mine = await CreateForAsync(hr, worker.Id, new DateOnly(2025, 7, 7));
        var other = await CreateForAsync(hr, worker.Id, new DateOnly(2025, 8, 4));
        var notMine = await CreateForAsync(hr, outsider.Id, new DateOnly(2025, 7, 7));
        using var manager = factory.CreatePortalClient(bossEmail);

        var profile = await manager.GetFromJsonAsync<MyProfileResponse>(new Uri("/api/v1/me/employee", UriKind.Relative), Cancellation);
        var team = await manager.GetFromJsonAsync<List<LeaveRequestResponse>>(new Uri("/api/v1/me/team/leave-requests?state=Pending", UriKind.Relative), Cancellation);
        using var approved = await DecideAsync(manager, mine.Id, "approve", null);
        using var rejectedWithoutReason = await DecideAsync(manager, other.Id, "reject", null);
        using var foreign = await DecideAsync(manager, notMine.Id, "approve", null);

        profile!.TeamSize.ShouldBe(1);
        team!.Select(request => request.Id).ShouldBe([other.Id, mine.Id], ignoreOrder: true);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await approved.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!.State.ShouldBe(LeaveState.Approved);
        await ShouldFailAsync(rejectedWithoutReason, HttpStatusCode.BadRequest, "leave.reason_required");
        await ShouldFailAsync(foreign, HttpStatusCode.NotFound, "leave.not_found");
    }

    private static string Email(string name) => $"{name}-{Guid.NewGuid():N}@acme.test";

    private static Task<HttpResponseMessage> DecideAsync(HttpClient client, Guid id, string action, string? note) =>
        client.PostAsJsonAsync(new Uri($"/api/v1/me/team/leave-requests/{id}/{action}", UriKind.Relative), new LeaveDecisionRequest(note), Cancellation);

    private static async Task<LeaveRequestResponse> CreateForAsync(HttpClient hr, Guid employeeId, DateOnly day)
    {
        using var response = await hr.PostAsJsonAsync(
            new Uri("/api/v1/leave-requests", UriKind.Relative),
            new CreateLeaveRequest(employeeId, LeaveKind.Unpaid, day, day, null),
            Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!;
    }

    private static async Task<EmployeeResponse> HireAsync(HttpClient hr, string firstName, string lastName, string email, Guid? managerId)
    {
        var number = "M" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using var response = await hr.PostAsJsonAsync(
            new Uri("/api/v1/employees", UriKind.Relative),
            new SaveEmployeeRequest(number, firstName, lastName, email, null, HireDate, null, null, managerId),
            Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation))!;
    }

    private static async Task SetEntitlementAsync(HttpClient hr, Guid employeeId, int days)
    {
        using var response = await hr.PutAsJsonAsync(
            new Uri($"/api/v1/employees/{employeeId}/leave/2025", UriKind.Relative), new SaveLeaveEntitlementRequest(days, 0), Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancellation));
    }

    private static async Task ShouldFailAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        response.StatusCode.ShouldBe(status, body);
        body.ShouldContain(code);
    }
}
