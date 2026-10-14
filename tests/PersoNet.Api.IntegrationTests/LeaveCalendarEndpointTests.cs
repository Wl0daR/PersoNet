using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.Organization;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class LeaveCalendarEndpointTests(PersoNetApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hr_sees_who_is_away_in_the_month_in_a_department_and_the_ones_under_it()
    {
        // A company of its own, so the calendar holds only the people of this test.
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var sales = await CreateDepartmentAsync(hr, "Sprzedaż", "SPR", null);
        var north = await CreateDepartmentAsync(hr, "Sprzedaż Północ", "SPN", sales.Id);
        var it = await CreateDepartmentAsync(hr, "IT", "IT", null);
        var ala = await HireAsync(hr, "Ala", "Nowak", null, sales.Id, null, new DateOnly(2024, 1, 1));
        var bartek = await HireAsync(hr, "Bartek", "Adamski", null, north.Id, null, new DateOnly(2024, 1, 1));
        var celina = await HireAsync(hr, "Celina", "Zięba", null, it.Id, null, new DateOnly(2024, 1, 1));
        await HireAsync(hr, "Daria", "Później", null, sales.Id, null, new DateOnly(2025, 7, 1));

        var approved = await CreateLeaveAsync(hr, ala.Id, new DateOnly(2025, 5, 28), new DateOnly(2025, 6, 3));
        await DecideAsync(hr, approved.Id, "approve");
        var pending = await CreateLeaveAsync(hr, bartek.Id, new DateOnly(2025, 6, 16), new DateOnly(2025, 6, 17));
        var rejected = await CreateLeaveAsync(hr, bartek.Id, new DateOnly(2025, 6, 23), new DateOnly(2025, 6, 23));
        await DecideAsync(hr, rejected.Id, "reject", "Inwentaryzacja");
        await CreateLeaveAsync(hr, celina.Id, new DateOnly(2025, 6, 9), new DateOnly(2025, 6, 9));
        await CreateLeaveAsync(hr, ala.Id, new DateOnly(2025, 7, 7), new DateOnly(2025, 7, 7));

        var calendar = await hr.GetFromJsonAsync<LeaveCalendarResponse>(
            new Uri($"/api/v1/leave-calendar?year=2025&month=6&departmentId={sales.Id}", UriKind.Relative), Cancellation);
        var company = await hr.GetFromJsonAsync<LeaveCalendarResponse>(new Uri("/api/v1/leave-calendar?year=2025&month=6", UriKind.Relative), Cancellation);
        using var badMonth = await hr.GetAsync(new Uri("/api/v1/leave-calendar?year=2025&month=13", UriKind.Relative), Cancellation);
        using var noDepartment = await hr.GetAsync(new Uri($"/api/v1/leave-calendar?year=2025&month=6&departmentId={Guid.NewGuid()}", UriKind.Relative), Cancellation);

        // Daria is hired after June; IT is not under Sales. Rejected leave and leave of other months are left out.
        calendar!.People.Select(person => person.LastName).ShouldBe(["Adamski", "Nowak"]);
        calendar.People[0].DepartmentName.ShouldBe("Sprzedaż Północ");
        calendar.People[0].Leave.ShouldBe([new LeaveCalendarEntry(pending.Id, LeaveKind.Unpaid, new DateOnly(2025, 6, 16), new DateOnly(2025, 6, 17), LeaveState.Pending, false, pending.Hours)]);
        calendar.People[1].Leave.Single().State.ShouldBe(LeaveState.Approved);
        calendar.People[1].Leave.Single().StartDate.ShouldBe(new DateOnly(2025, 5, 28));
        calendar.Holidays.Select(holiday => holiday.Date).ShouldBe([new DateOnly(2025, 6, 8), new DateOnly(2025, 6, 19)]);
        company!.People.Select(person => person.LastName).ShouldBe(["Adamski", "Nowak", "Zięba"]);
        badMonth.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        noDepartment.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Manager_sees_themselves_and_their_team_and_the_company_calendar_is_for_hr()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var email = $"szefowa-{Guid.NewGuid():N}@acme.test";
        var boss = await HireAsync(hr, "Ewa", "Szefowa", email, null, null, new DateOnly(2024, 1, 1));
        var member = await HireAsync(hr, "Jan", "Członek", null, null, boss.Id, new DateOnly(2024, 1, 1));
        await HireAsync(hr, "Obcy", "Spoza", null, null, null, new DateOnly(2024, 1, 1));
        await CreateLeaveAsync(hr, member.Id, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 4));
        using var manager = factory.CreateSignedInClient(tenantId, "employee");
        manager.DefaultRequestHeaders.Add(TestAuthHandler.EmailHeader, email);

        var team = await manager.GetFromJsonAsync<LeaveCalendarResponse>(new Uri("/api/v1/me/team/calendar?year=2025&month=6", UriKind.Relative), Cancellation);
        using var company = await manager.GetAsync(new Uri("/api/v1/leave-calendar?year=2025&month=6", UriKind.Relative), Cancellation);

        team!.People.Select(person => person.FirstName).ShouldBe(["Jan", "Ewa"]);
        team.People[0].Leave.Single().Kind.ShouldBe(LeaveKind.Unpaid);
        company.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    private static async Task<DepartmentResponse> CreateDepartmentAsync(HttpClient hr, string name, string code, Guid? parentId)
    {
        using var response = await hr.PostAsJsonAsync(new Uri("/api/v1/organization/departments", UriKind.Relative), new CreateDepartmentRequest(name, code, parentId), Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<DepartmentResponse>(Cancellation))!;
    }

    private static async Task<EmployeeResponse> HireAsync(
        HttpClient hr, string firstName, string lastName, string? email, Guid? departmentId, Guid? managerId, DateOnly hireDate)
    {
        var number = "K" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using var response = await hr.PostAsJsonAsync(
            new Uri("/api/v1/employees", UriKind.Relative),
            new SaveEmployeeRequest(number, firstName, lastName, email, null, hireDate, departmentId, null, managerId),
            Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation))!;
    }

    private static async Task<LeaveRequestResponse> CreateLeaveAsync(HttpClient hr, Guid employeeId, DateOnly start, DateOnly end)
    {
        using var response = await hr.PostAsJsonAsync(
            new Uri("/api/v1/leave-requests", UriKind.Relative), new CreateLeaveRequest(employeeId, LeaveKind.Unpaid, start, end, null), Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!;
    }

    private static async Task DecideAsync(HttpClient hr, Guid id, string action, string? note = null)
    {
        using var response = await hr.PostAsJsonAsync(new Uri($"/api/v1/leave-requests/{id}/{action}", UriKind.Relative), new LeaveDecisionRequest(note), Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancellation));
    }
}
