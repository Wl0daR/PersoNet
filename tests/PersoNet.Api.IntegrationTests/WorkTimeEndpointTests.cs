using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Company;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Contracts.WorkTime;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class WorkTimeEndpointTests(PersoNetApiFactory factory)
{
    private static readonly DateOnly Hired = new(2025, 1, 1);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Filling_the_month_from_the_plan_skips_leave_and_the_totals_show_the_norm_of_the_month()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var ala = await HireWithContractAsync(hr, "Ala", 1m);
        await PutAsync(hr, $"/api/v1/employees/{ala.Id}/leave/2025", new SaveLeaveEntitlementRequest(26, 0));
        var leave = await PostAsync<LeaveRequestResponse>(hr, "/api/v1/leave-requests", new CreateLeaveRequest(ala.Id, LeaveKind.Annual, Day(3), Day(4), null), HttpStatusCode.Created);
        await PostAsync<LeaveRequestResponse>(hr, $"/api/v1/leave-requests/{leave.Id}/approve", new LeaveDecisionRequest(null));

        var sheet = await PostAsync<TimesheetResponse>(hr, $"/api/v1/employees/{ala.Id}/work-time/2025/11/fill", new { });

        // November 2025: 19 working days, two of them on leave; the holiday on Saturday the 1st lowers the norm to 144 hours.
        sheet.Totals.NormHours.ShouldBe(144m);
        sheet.Totals.PlannedHours.ShouldBe(152m);
        sheet.Totals.WorkedHours.ShouldBe(136m);
        sheet.Totals.LeaveHours.ShouldBe(16m);
        sheet.Totals.DaysWorked.ShouldBe(17);
        sheet.Source.ShouldBe(WorkTimeSourceKind.Manual);
        var onLeave = sheet.Days[2];
        onLeave.Leave.ShouldBe(LeaveKind.Annual);
        onLeave.AbsenceHours.ShouldBe(8m);
        onLeave.IsRecorded.ShouldBeFalse();
        var worked = sheet.Days[4];
        (worked.Start, worked.End, worked.WorkedHours, worked.IsManual).ShouldBe((new TimeOnly(8, 0), new TimeOnly(16, 0), 8m, true));
        sheet.Days[10].Kind.ShouldBe(TimesheetDayKind.Holiday);
        sheet.Days[10].HolidayName.ShouldBe("Narodowe Święto Niepodległości");
    }

    [Fact]
    public async Task Hr_types_hours_and_absences_and_overtime_and_night_work_are_worked_out()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var ala = await HireWithContractAsync(hr, "Ala", 1m);
        var days = $"/api/v1/employees/{ala.Id}/work-time/days";

        await PutAsync(hr, $"{days}/2025-11-05", new SaveWorkDayRequest(new TimeOnly(14, 0), new TimeOnly(23, 30), 30, null, "inwentaryzacja"));
        var sheet = await PutAsync<TimesheetResponse>(hr, $"{days}/2025-11-06", new SaveWorkDayRequest(null, null, 0, AbsenceKind.Sickness, null));
        using var noEnd = await hr.PutAsJsonAsync(new Uri($"{days}/2025-11-07", UriKind.Relative), new SaveWorkDayRequest(new TimeOnly(8, 0), null, 0, null, null), Cancellation);
        using var beforeHire = await hr.PutAsJsonAsync(new Uri($"{days}/2024-12-31", UriKind.Relative), new SaveWorkDayRequest(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, null, null), Cancellation);
        using var cleared = await hr.DeleteAsync(new Uri($"{days}/2025-11-06", UriKind.Relative), Cancellation);
        var afterClear = (await cleared.Content.ReadFromJsonAsync<TimesheetResponse>(Cancellation))!;

        var late = sheet.Days[4];
        (late.WorkedHours, late.OvertimeHours, late.NightHours, late.BreakMinutes, late.Note).ShouldBe((9m, 1m, 1.5m, 30, "inwentaryzacja"));
        sheet.Days[5].Absence.ShouldBe(AbsenceKind.Sickness);
        sheet.Days[5].AbsenceHours.ShouldBe(8m);
        sheet.Totals.AbsenceHours.ShouldBe(8m);
        sheet.Totals.OvertimeHours.ShouldBe(1m);
        await ShouldFailAsync(noEnd, HttpStatusCode.BadRequest, "worktime.times_required");
        await ShouldFailAsync(beforeHire, HttpStatusCode.BadRequest, "worktime.not_employed");
        cleared.StatusCode.ShouldBe(HttpStatusCode.OK);
        afterClear.Days[5].IsRecorded.ShouldBeFalse();
    }

    [Fact]
    public async Task Hours_cannot_be_typed_on_a_day_of_approved_leave()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var ala = await HireWithContractAsync(hr, "Ala", 1m);
        var leave = await PostAsync<LeaveRequestResponse>(hr, "/api/v1/leave-requests", new CreateLeaveRequest(ala.Id, LeaveKind.Unpaid, Day(3), Day(3), null), HttpStatusCode.Created);
        await PostAsync<LeaveRequestResponse>(hr, $"/api/v1/leave-requests/{leave.Id}/approve", new LeaveDecisionRequest(null));

        using var response = await hr.PutAsJsonAsync(
            new Uri($"/api/v1/employees/{ala.Id}/work-time/days/2025-11-03", UriKind.Relative),
            new SaveWorkDayRequest(new TimeOnly(8, 0), new TimeOnly(16, 0), 0, null, null),
            Cancellation);

        await ShouldFailAsync(response, HttpStatusCode.Conflict, "worktime.leave_on_day");
    }

    [Fact]
    public async Task Clock_events_are_matched_by_the_time_clock_id_paired_into_days_and_imported_only_once()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var ala = await HireWithContractAsync(hr, "Ala", 1m);
        var bartek = await HireWithContractAsync(hr, "Bartek", 1m);
        await PutAsync(hr, $"/api/v1/employees/{ala.Id}/clock-id", new SaveClockIdRequest(" 0042 "), HttpStatusCode.NoContent);
        using var taken = await hr.PutAsJsonAsync(new Uri($"/api/v1/employees/{bartek.Id}/clock-id", UriKind.Relative), new SaveClockIdRequest("0042"), Cancellation);

        var import = new ImportClockEventsRequest(ClockEventOrigin.File,
        [
            Event("0042", "2025-11-05 07:55:12", ClockEventDirection.In, "Wejście główne"),
            Event("0042", "2025-11-05 16:05:00", ClockEventDirection.Out),
            Event("0042", "2025-11-06 22:00:00", ClockEventDirection.In),
            Event("0042", "2025-11-07 06:00:00", ClockEventDirection.Out),
            Event("0042", "2025-11-10 08:00:00", ClockEventDirection.In),
            Event("9999", "2025-11-05 08:00:00", ClockEventDirection.In),
        ]);
        var first = await PostAsync<ImportClockEventsResponse>(hr, "/api/v1/clock-events/import", import);
        var again = await PostAsync<ImportClockEventsResponse>(hr, "/api/v1/clock-events/import", import);
        var sheet = await GetSheetAsync(hr, ala.Id);

        await ShouldFailAsync(taken, HttpStatusCode.Conflict, "worktime.clock_id_taken");
        (first.Received, first.Added, first.Duplicates, first.DaysUpdated).ShouldBe((6, 5, 0, 3));
        first.UnknownClockIds.ShouldBe(["9999"]);
        (again.Added, again.Duplicates).ShouldBe((0, 5));
        sheet.ClockId.ShouldBe("0042");
        var day = sheet.Days[4];
        (day.Start, day.End, day.WorkedHours, day.OvertimeHours, day.IsManual).ShouldBe((new TimeOnly(7, 55), new TimeOnly(16, 5), 8.17m, 0.17m, false));
        day.Events.Select(e => (e.Direction, e.Device)).ShouldBe([(ClockEventDirection.In, "Wejście główne"), (ClockEventDirection.Out, null)]);
        (sheet.Days[5].WorkedHours, sheet.Days[5].NightHours).ShouldBe((8m, 8m));
        sheet.Days[6].IsRecorded.ShouldBeFalse();
        (sheet.Days[9].Issue, sheet.Days[9].WorkedHours).ShouldBe((ClockIssue.MissingOut, 0m));
        sheet.Totals.DaysWithIssues.ShouldBe(1);
    }

    [Fact]
    public async Task A_day_corrected_by_hand_keeps_its_hours_until_the_correction_is_removed()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var ala = await HireWithContractAsync(hr, "Ala", 1m);
        await PutAsync(hr, $"/api/v1/employees/{ala.Id}/clock-id", new SaveClockIdRequest("A-1"), HttpStatusCode.NoContent);
        await PostAsync<ImportClockEventsResponse>(hr, "/api/v1/clock-events/import", new ImportClockEventsRequest(ClockEventOrigin.Integration, [Event("A-1", "2025-11-10 08:00:00", ClockEventDirection.In)]));

        // HR types the day; the missing clock-out arrives later and does not change it.
        await PutAsync(hr, $"/api/v1/employees/{ala.Id}/work-time/days/2025-11-10", new SaveWorkDayRequest(new TimeOnly(8, 0), new TimeOnly(15, 0), 0, null, "zapomniała odbić wyjścia"));
        await PostAsync<ImportClockEventsResponse>(hr, "/api/v1/clock-events/import", new ImportClockEventsRequest(ClockEventOrigin.Integration, [Event("A-1", "2025-11-10 16:00:00", ClockEventDirection.Out)]));
        var corrected = await GetSheetAsync(hr, ala.Id);
        using var cleared = await hr.DeleteAsync(new Uri($"/api/v1/employees/{ala.Id}/work-time/days/2025-11-10", UriKind.Relative), Cancellation);
        var fromClock = (await cleared.Content.ReadFromJsonAsync<TimesheetResponse>(Cancellation))!;

        (corrected.Days[9].End, corrected.Days[9].IsManual).ShouldBe((new TimeOnly(15, 0), true));
        (fromClock.Days[9].End, fromClock.Days[9].IsManual, fromClock.Days[9].Issue).ShouldBe((new TimeOnly(16, 0), false, ClockIssue.None));
    }

    [Fact]
    public async Task The_company_chooses_where_working_time_comes_from()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var ala = await HireWithContractAsync(hr, "Ala", 1m);

        var company = await PutAsync<CompanyResponse>(hr, "/api/v1/company/work-time", new SaveWorkTimeSettingsRequest(WorkTimeSourceKind.RogerPrMaster));
        var sheet = await GetSheetAsync(hr, ala.Id);

        company.WorkTimeSource.ShouldBe(WorkTimeSourceKind.RogerPrMaster);
        sheet.Source.ShouldBe(WorkTimeSourceKind.RogerPrMaster);
    }

    [Fact]
    public async Task Working_time_needs_its_own_permissions()
    {
        using var employee = factory.CreateSignedInClient(null, "employee");

        using var read = await employee.GetAsync(new Uri($"/api/v1/employees/{Guid.NewGuid()}/work-time/2025/11", UriKind.Relative), Cancellation);
        using var import = await employee.PostAsJsonAsync(
            new Uri("/api/v1/clock-events/import", UriKind.Relative),
            new ImportClockEventsRequest(ClockEventOrigin.File, [Event("1", "2025-11-03 08:00:00", ClockEventDirection.In)]),
            Cancellation);

        read.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        import.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Days_worked_in_the_earlier_system_are_imported_once_and_not_over_approved_leave()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var ala = await HireWithContractAsync(hr, "Ala", 1m);
        await PutAsync(hr, $"/api/v1/employees/{ala.Id}/leave/2025", new SaveLeaveEntitlementRequest(26, 0));
        var leave = await PostAsync<LeaveRequestResponse>(hr, "/api/v1/leave-requests", new CreateLeaveRequest(ala.Id, LeaveKind.Annual, Day(4), Day(4), null), HttpStatusCode.Created);
        await PostAsync<LeaveRequestResponse>(hr, $"/api/v1/leave-requests/{leave.Id}/approve", new LeaveDecisionRequest(null));
        var request = new ImportWorkDaysRequest(
        [
            new ImportWorkDayRow(1, ala.Number.ToLowerInvariant(), Day(3), new TimeOnly(6, 0), new TimeOnly(14, 0)),
            new ImportWorkDayRow(2, ala.Number, Day(4), new TimeOnly(6, 0), new TimeOnly(14, 0)),
            new ImportWorkDayRow(3, ala.Number, Day(5), new TimeOnly(22, 0), new TimeOnly(6, 0)),
            new ImportWorkDayRow(4, ala.Number, Day(6), new TimeOnly(8, 0), new TimeOnly(8, 0)),
            new ImportWorkDayRow(5, "NIEMA", Day(3), new TimeOnly(6, 0), new TimeOnly(14, 0)),
        ]);

        var first = await PostAsync<ImportWorkDaysResponse>(hr, "/api/v1/work-days/import", request);
        var again = await PostAsync<ImportWorkDaysResponse>(hr, "/api/v1/work-days/import", request);
        using var empty = await hr.PostAsJsonAsync(new Uri("/api/v1/work-days/import", UriKind.Relative), new ImportWorkDaysRequest([]), Cancellation);
        var sheet = await GetSheetAsync(hr, ala.Id);

        first.Added.ShouldBe(2);
        first.Existing.ShouldBe(0);
        first.Problems.Select(problem => problem.Line).ShouldBe([2, 4, 5]);
        first.Problems[0].Message.ShouldContain("zatwierdzony urlop");
        again.Added.ShouldBe(0);
        again.Existing.ShouldBe(2);
        await ShouldFailAsync(empty, HttpStatusCode.BadRequest, "worktime.days_import_empty");
        (sheet.Days[2].Start, sheet.Days[2].End, sheet.Days[2].WorkedHours).ShouldBe((new TimeOnly(6, 0), new TimeOnly(14, 0), 8m));
        (sheet.Days[4].Start, sheet.Days[4].End, sheet.Days[4].WorkedHours).ShouldBe((new TimeOnly(22, 0), new TimeOnly(6, 0), 8m));
        sheet.Days[3].IsRecorded.ShouldBeFalse();
    }

    private static DateOnly Day(int day) => new(2025, 11, day);

    private static ClockEventInput Event(string clockId, string at, ClockEventDirection direction, string? device = null) =>
        new(null, clockId, DateTime.Parse(at, System.Globalization.CultureInfo.InvariantCulture), direction, device);

    private static async Task<TimesheetResponse> GetSheetAsync(HttpClient hr, Guid employeeId) =>
        (await hr.GetFromJsonAsync<TimesheetResponse>(new Uri($"/api/v1/employees/{employeeId}/work-time/2025/11", UriKind.Relative), Cancellation))!;

    private static async Task<EmployeeResponse> HireWithContractAsync(HttpClient hr, string firstName, decimal workingTime)
    {
        var number = "W" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var employee = await PostAsync<EmployeeResponse>(
            hr, "/api/v1/employees", new SaveEmployeeRequest(number, firstName, "Nowak", null, null, Hired, null, null, null), HttpStatusCode.Created);
        await PostAsync<ContractResponse>(
            hr, $"/api/v1/employees/{employee.Id}/contracts", new SaveContractRequest(ContractKind.Indefinite, Hired, null, workingTime), HttpStatusCode.Created);
        return employee;
    }

    private static async Task<T> PostAsync<T>(HttpClient client, string uri, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.PostAsJsonAsync(new Uri(uri, UriKind.Relative), body, Cancellation);
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<T>(Cancellation))!;
    }

    private static async Task PutAsync(HttpClient client, string uri, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.PutAsJsonAsync(new Uri(uri, UriKind.Relative), body, Cancellation);
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Cancellation));
    }

    private static async Task<T> PutAsync<T>(HttpClient client, string uri, object body)
    {
        using var response = await client.PutAsJsonAsync(new Uri(uri, UriKind.Relative), body, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<T>(Cancellation))!;
    }

    private static async Task ShouldFailAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        response.StatusCode.ShouldBe(status, body);
        body.ShouldContain(code);
    }
}
