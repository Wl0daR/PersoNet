using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Absences;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class AbsenceEndpointTests(PersoNetApiFactory factory)
{
    private static readonly DateOnly HireDate = new(2020, 1, 2);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hr_records_changes_and_removes_an_absence_which_counts_calendar_and_working_days()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);

        // A sick note over a weekend: 7 calendar days, 5 working days.
        var sickness = await CreateAsync(hr, employee.Id, new SaveAbsenceRequest(AbsenceKind.Sickness, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 9), "e-ZLA"));
        using var overlap = await hr.PostAsJsonAsync(
            Absences(employee.Id), new SaveAbsenceRequest(AbsenceKind.RemoteWork, new DateOnly(2025, 3, 9), new DateOnly(2025, 3, 10), null), Cancellation);
        using var backwards = await hr.PostAsJsonAsync(
            Absences(employee.Id), new SaveAbsenceRequest(AbsenceKind.Training, new DateOnly(2025, 3, 20), new DateOnly(2025, 3, 19), null), Cancellation);
        using var changed = await hr.PutAsJsonAsync(
            new Uri($"/api/v1/absences/{sickness.Id}", UriKind.Relative),
            new SaveAbsenceRequest(AbsenceKind.ChildCareAllowance, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 4), null),
            Cancellation);
        var of2025 = await ListAsync(hr, employee.Id, 2025);
        var of2024 = await ListAsync(hr, employee.Id, 2024);
        var json = await hr.GetStringAsync(Absences(employee.Id), Cancellation);

        using var other = factory.CreateSignedInClient(await factory.CreateTenantAsync());
        using var foreign = await other.DeleteAsync(new Uri($"/api/v1/absences/{sickness.Id}", UriKind.Relative), Cancellation);
        using var removed = await hr.DeleteAsync(new Uri($"/api/v1/absences/{sickness.Id}", UriKind.Relative), Cancellation);
        var after = await ListAsync(hr, employee.Id, null);

        sickness.CalendarDays.ShouldBe(7);
        sickness.WorkingDays.ShouldBe(5);
        sickness.Note.ShouldBe("e-ZLA");
        await ShouldFailAsync(overlap, HttpStatusCode.Conflict, "absence.overlap");
        await ShouldFailAsync(backwards, HttpStatusCode.BadRequest, "absence.end_before_start");
        changed.StatusCode.ShouldBe(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync(Cancellation));
        of2025.ShouldHaveSingleItem().Kind.ShouldBe(AbsenceKind.ChildCareAllowance);
        of2025[0].CalendarDays.ShouldBe(2);
        of2024.ShouldBeEmpty();
        json.ShouldContain("\"ChildCareAllowance\"");
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        after.ShouldBeEmpty();
    }

    [Fact]
    public async Task Leave_history_is_imported_as_approved_leave_split_at_the_turn_of_the_year_and_can_be_imported_again()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr);
        var request = new ImportLeaveHistoryRequest(
            [
                new ImportLeaveRow(1, employee.Number.ToLowerInvariant(), LeaveKind.Annual, new DateOnly(2024, 12, 23), new DateOnly(2025, 1, 3)),
                new ImportLeaveRow(2, employee.Number, LeaveKind.Maternity, new DateOnly(2025, 2, 3), new DateOnly(2025, 2, 28)),
                new ImportLeaveRow(3, employee.Number, LeaveKind.OnDemand, new DateOnly(2025, 2, 10), new DateOnly(2025, 2, 10)),
                new ImportLeaveRow(4, employee.Number, LeaveKind.Annual, new DateOnly(2025, 3, 8), new DateOnly(2025, 3, 9)),
                new ImportLeaveRow(5, "NIEMA", LeaveKind.Annual, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3)),
            ],
            [
                new ImportAbsenceRow(6, employee.Number, AbsenceKind.Sickness, new DateOnly(2025, 4, 7), new DateOnly(2025, 4, 11)),
                new ImportAbsenceRow(7, employee.Number, AbsenceKind.RemoteWork, new DateOnly(2025, 4, 10), new DateOnly(2025, 4, 10)),
                new ImportAbsenceRow(8, employee.Number, AbsenceKind.BusinessTrip, new DateOnly(2025, 5, 5), new DateOnly(2025, 5, 4)),
            ],
            [
                new ImportEntitlementRow(9, employee.Number, 2025, 26, 3),
                new ImportEntitlementRow(10, employee.Number, 2024, 70, 0),
            ]);

        var first = await ImportAsync(hr, request);
        var again = await ImportAsync(hr, request);
        var leave = (await hr.GetFromJsonAsync<List<LeaveRequestResponse>>(new Uri($"/api/v1/leave-requests?employeeId={employee.Id}", UriKind.Relative), Cancellation))!;
        var balance = (await hr.GetFromJsonAsync<LeaveBalanceResponse>(new Uri($"/api/v1/employees/{employee.Id}/leave/2025", UriKind.Relative), Cancellation))!;
        var absences = await ListAsync(hr, employee.Id, 2025);

        // 23–31 Dec and 1–3 Jan (2 working days, 1 Jan is a holiday) become two requests; maternity leave, two absences and a year's entitlement.
        first.LeaveAdded.ShouldBe(3);
        first.AbsencesAdded.ShouldBe(2);
        first.EntitlementsSaved.ShouldBe(1);
        first.Existing.ShouldBe(0);
        first.Problems.Select(problem => problem.Line).ShouldBe([3, 4, 5, 8, 10]);
        first.Problems.Single(problem => problem.Line == 3).Message.ShouldContain("nakłada się");
        first.Problems.Single(problem => problem.Line == 4).Message.ShouldContain("dnia roboczego");
        first.Problems.Single(problem => problem.Line == 5).Message.ShouldContain("NIEMA");
        again.LeaveAdded.ShouldBe(0);
        again.Existing.ShouldBe(6);
        leave.Count.ShouldBe(3);
        leave.ShouldAllBe(request => request.State == LeaveState.Approved);
        leave.Select(request => (request.StartDate, request.EndDate, request.Days)).ShouldBe(
            [(new DateOnly(2025, 2, 3), new DateOnly(2025, 2, 28), 20), (new DateOnly(2025, 1, 1), new DateOnly(2025, 1, 3), 2), (new DateOnly(2024, 12, 23), new DateOnly(2024, 12, 31), 5)],
            ignoreOrder: true);
        balance.EntitlementDays.ShouldBe(26);
        balance.CarriedOverDays.ShouldBe(3);
        balance.UsedHours.ShouldBe(16m);
        absences.Count.ShouldBe(2);
    }

    [Fact]
    public async Task An_import_with_nothing_in_it_is_refused()
    {
        using var hr = factory.CreateSignedInClient();

        using var empty = await hr.PostAsJsonAsync(new Uri("/api/v1/leave/import", UriKind.Relative), new ImportLeaveHistoryRequest([], [], []), Cancellation);

        await ShouldFailAsync(empty, HttpStatusCode.BadRequest, "leave.import_empty");
    }

    private static Uri Absences(Guid employeeId) => new($"/api/v1/employees/{employeeId}/absences", UriKind.Relative);

    private static async Task<AbsenceResponse> CreateAsync(HttpClient client, Guid employeeId, SaveAbsenceRequest request)
    {
        using var response = await client.PostAsJsonAsync(Absences(employeeId), request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<AbsenceResponse>(Cancellation))!;
    }

    private static async Task<List<AbsenceResponse>> ListAsync(HttpClient client, Guid employeeId, int? year) =>
        (await client.GetFromJsonAsync<List<AbsenceResponse>>(
            new Uri(Absences(employeeId) + (year is { } y ? $"?year={y}" : string.Empty), UriKind.Relative), Cancellation))!;

    private static async Task<ImportLeaveHistoryResponse> ImportAsync(HttpClient client, ImportLeaveHistoryRequest request)
    {
        using var response = await client.PostAsJsonAsync(new Uri("/api/v1/leave/import", UriKind.Relative), request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<ImportLeaveHistoryResponse>(Cancellation))!;
    }

    private static async Task ShouldFailAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        response.StatusCode.ShouldBe(status, body);
        body.ShouldContain(code);
    }

    private static async Task<EmployeeResponse> HireAsync(HttpClient client)
    {
        var number = "N" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using var response = await client.PostAsJsonAsync(
            new Uri("/api/v1/employees", UriKind.Relative),
            new SaveEmployeeRequest(number, "Anna", "Nieobecna", null, null, HireDate, null, null, null),
            Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation))!;
    }
}
