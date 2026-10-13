using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class LeaveEndpointTests(PersoNetApiFactory factory)
{
    private const string Requests = "/api/v1/leave-requests";
    private static readonly DateOnly HireDate = new(2024, 3, 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Annual_leave_counts_working_days_and_moves_from_pending_to_used_when_approved()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);
        await SetEntitlementAsync(hr, employee.Id, 26, 2);

        // 1 May is a holiday and 3–4 May a weekend, so this is 6 working days.
        var request = await CreateAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.Annual, new DateOnly(2025, 4, 28), new DateOnly(2025, 5, 6), "Majówka"));
        var pending = await BalanceAsync(hr, employee.Id);

        using var approve = await hr.PostAsJsonAsync(new Uri($"{Requests}/{request.Id}/approve", UriKind.Relative), new LeaveDecisionRequest(null), Cancellation);
        var approved = await approve.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation);
        var after = await BalanceAsync(hr, employee.Id);

        request.Days.ShouldBe(6);
        request.State.ShouldBe(LeaveState.Pending);
        request.EmployeeName.ShouldBe("Urlopowy Jan");
        pending.PendingDays.ShouldBe(6);
        pending.RemainingDays.ShouldBe(22);
        approve.StatusCode.ShouldBe(HttpStatusCode.OK);
        approved!.State.ShouldBe(LeaveState.Approved);
        approved.DecidedAt.ShouldNotBeNull();
        after.UsedDays.ShouldBe(6);
        after.PendingDays.ShouldBe(0);
        after.RemainingDays.ShouldBe(22);
    }

    [Fact]
    public async Task Annual_leave_needs_an_entitlement_and_enough_days_left()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);

        using var noEntitlement = await PostAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.Annual, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6), null));
        await SetEntitlementAsync(hr, employee.Id, 4, 0);
        using var tooLong = await PostAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.Annual, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6), null));
        using var unpaid = await PostAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.Unpaid, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6), null));

        await ShouldFailAsync(noEntitlement, HttpStatusCode.BadRequest, "leave.no_entitlement");
        await ShouldFailAsync(tooLong, HttpStatusCode.BadRequest, "leave.not_enough_days");
        unpaid.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Overlaps_and_yearly_caps_are_enforced()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);
        await SetEntitlementAsync(hr, employee.Id, 26, 0);
        await CreateAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.OnDemand, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 5), null));

        using var overlap = await PostAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.Annual, new DateOnly(2025, 3, 5), new DateOnly(2025, 3, 7), null));
        using var onDemand = await PostAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.OnDemand, new DateOnly(2025, 3, 10), new DateOnly(2025, 3, 11), null));
        using var childCare = await PostAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.ChildCare, new DateOnly(2025, 4, 7), new DateOnly(2025, 4, 9), null));

        await ShouldFailAsync(overlap, HttpStatusCode.Conflict, "leave.overlap");
        await ShouldFailAsync(onDemand, HttpStatusCode.BadRequest, "leave.on_demand_limit");
        await ShouldFailAsync(childCare, HttpStatusCode.BadRequest, "leave.child_care_limit");
    }

    [Theory]
    [InlineData("2025-05-10", "2025-05-11", "leave.no_working_days")]
    [InlineData("2025-12-29", "2026-01-02", "leave.crosses_year")]
    [InlineData("2025-06-06", "2025-06-02", "leave.end_before_start")]
    [InlineData("2024-02-05", "2024-02-06", "leave.outside_employment")]
    public async Task Periods_that_cannot_be_leave_are_refused(string start, string end, string code)
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);

        using var response = await PostAsync(hr, new CreateLeaveRequest(
            employee.Id, LeaveKind.Unpaid, DateOnly.Parse(start, System.Globalization.CultureInfo.InvariantCulture), DateOnly.Parse(end, System.Globalization.CultureInfo.InvariantCulture), null));

        await ShouldFailAsync(response, HttpStatusCode.BadRequest, code);
    }

    [Fact]
    public async Task Rejection_needs_a_reason_a_decision_is_final_and_cancelling_returns_the_days()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);
        await SetEntitlementAsync(hr, employee.Id, 20, 0);
        var first = await CreateAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.Annual, new DateOnly(2025, 7, 7), new DateOnly(2025, 7, 11), null));
        var second = await CreateAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.Annual, new DateOnly(2025, 8, 4), new DateOnly(2025, 8, 8), null));

        using var noReason = await DecideAsync(hr, first.Id, "reject", " ");
        using var rejected = await DecideAsync(hr, first.Id, "reject", "Inwentaryzacja w lipcu");
        using var approveRejected = await DecideAsync(hr, first.Id, "approve", null);
        using var approved = await DecideAsync(hr, second.Id, "approve", null);
        using var cancelled = await DecideAsync(hr, second.Id, "cancel", "Pracownik zmienił plany");
        var balance = await BalanceAsync(hr, employee.Id);

        await ShouldFailAsync(noReason, HttpStatusCode.BadRequest, "leave.reason_required");
        (await rejected.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!.DecisionNote.ShouldBe("Inwentaryzacja w lipcu");
        await ShouldFailAsync(approveRejected, HttpStatusCode.Conflict, "leave.already_decided");
        approved.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await cancelled.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!.State.ShouldBe(LeaveState.Cancelled);
        balance.UsedDays.ShouldBe(0);
        balance.RemainingDays.ShouldBe(20);
    }

    [Fact]
    public async Task List_shows_pending_first_with_state_as_text_and_hides_other_companies()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);
        var decided = await CreateAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.Unpaid, new DateOnly(2025, 9, 1), new DateOnly(2025, 9, 2), null));
        using var _ = await DecideAsync(hr, decided.Id, "approve", null);
        var waiting = await CreateAsync(hr, new CreateLeaveRequest(employee.Id, LeaveKind.Unpaid, new DateOnly(2025, 2, 3), new DateOnly(2025, 2, 4), null));

        var json = await hr.GetStringAsync(new Uri($"{Requests}?employeeId={employee.Id}", UriKind.Relative), Cancellation);
        var all = await hr.GetFromJsonAsync<List<LeaveRequestResponse>>(new Uri($"{Requests}?employeeId={employee.Id}&year=2025", UriKind.Relative), Cancellation);
        var pending = await hr.GetFromJsonAsync<List<LeaveRequestResponse>>(new Uri($"{Requests}?employeeId={employee.Id}&state=Pending", UriKind.Relative), Cancellation);

        using var other = factory.CreateSignedInClient(await factory.CreateTenantAsync());
        var foreign = await other.GetFromJsonAsync<List<LeaveRequestResponse>>(new Uri(Requests, UriKind.Relative), Cancellation);
        using var foreignApprove = await DecideAsync(other, waiting.Id, "approve", null);

        json.ShouldContain("\"Pending\"");
        all.ShouldNotBeNull();
        all.Select(request => request.Id).ShouldBe([waiting.Id, decided.Id]);
        pending!.ShouldHaveSingleItem().Id.ShouldBe(waiting.Id);
        foreign!.ShouldNotContain(request => request.EmployeeId == employee.Id);
        foreignApprove.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, CreateLeaveRequest request) =>
        client.PostAsJsonAsync(new Uri(Requests, UriKind.Relative), request, Cancellation);

    private static Task<HttpResponseMessage> DecideAsync(HttpClient client, Guid id, string action, string? note) =>
        client.PostAsJsonAsync(new Uri($"{Requests}/{id}/{action}", UriKind.Relative), new LeaveDecisionRequest(note), Cancellation);

    private static async Task ShouldFailAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        response.StatusCode.ShouldBe(status, body);
        body.ShouldContain(code);
    }

    private static async Task<LeaveRequestResponse> CreateAsync(HttpClient client, CreateLeaveRequest request)
    {
        using var response = await PostAsync(client, request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!;
    }

    private static async Task SetEntitlementAsync(HttpClient client, Guid employeeId, int days, int carriedOver)
    {
        using var response = await client.PutAsJsonAsync(
            new Uri($"/api/v1/employees/{employeeId}/leave/2025", UriKind.Relative), new SaveLeaveEntitlementRequest(days, carriedOver), Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancellation));
    }

    private static async Task<LeaveBalanceResponse> BalanceAsync(HttpClient client, Guid employeeId) =>
        (await client.GetFromJsonAsync<LeaveBalanceResponse>(new Uri($"/api/v1/employees/{employeeId}/leave/2025", UriKind.Relative), Cancellation))!;

    private static async Task<EmployeeResponse> HireAsync(HttpClient client)
    {
        var number = "U" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/employees", UriKind.Relative),
            new SaveEmployeeRequest(number, "Jan", "Urlopowy", null, null, HireDate, null, null, null),
            Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation))!;
    }
}
