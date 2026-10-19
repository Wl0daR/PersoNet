using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Organization;
using PersoNet.Contracts.Reports;
using PersoNet.Contracts.Safety;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class SafetyEndpointTests(PersoNetApiFactory factory)
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.Now);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hr_keeps_the_list_of_trainings_and_records_an_employee_s_trainings_and_examinations()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var production = await PostAsync<DepartmentResponse>(hr, "/api/v1/organization/departments", new CreateDepartmentRequest("Produkcja", "PROD", null), HttpStatusCode.Created);
        var assembly = await PostAsync<DepartmentResponse>(hr, "/api/v1/organization/departments", new CreateDepartmentRequest("Montaż", "MONT", production.Id), HttpStatusCode.Created);
        var office = await PostAsync<DepartmentResponse>(hr, "/api/v1/organization/departments", new CreateDepartmentRequest("Biuro", "BIU", null), HttpStatusCode.Created);
        var bhp = await PostAsync<TrainingCourseResponse>(hr, "/api/v1/training-courses", new SaveTrainingCourseRequest("BHP okresowe", 36, true, [], null), HttpStatusCode.Created);
        var cranes = await PostAsync<TrainingCourseResponse>(hr, "/api/v1/training-courses", new SaveTrainingCourseRequest("Suwnice", 60, true, [production.Id], "UDT"), HttpStatusCode.Created);
        await PostAsync<TrainingCourseResponse>(hr, "/api/v1/training-courses", new SaveTrainingCourseRequest("Excel", null, false, [office.Id], null), HttpStatusCode.Created);
        using var taken = await hr.PostAsJsonAsync(new Uri("/api/v1/training-courses", UriKind.Relative), new SaveTrainingCourseRequest("bhp OKRESOWE", 12, true, [], null), Cancellation);
        using var tooLong = await hr.PostAsJsonAsync(new Uri("/api/v1/training-courses", UriKind.Relative), new SaveTrainingCourseRequest("Wózki", 241, true, [], null), Cancellation);
        var worker = await HireAsync(hr, "Marek", assembly.Id);

        var training = await PostAsync<EmployeeTrainingResponse>(
            hr, $"/api/v1/employees/{worker.Id}/trainings", new SaveEmployeeTrainingRequest(bhp.Id, new DateOnly(2024, 3, 15), null, "zaświadczenie 12/2024"), HttpStatusCode.Created);
        var older = await PostAsync<EmployeeTrainingResponse>(
            hr, $"/api/v1/employees/{worker.Id}/trainings", new SaveEmployeeTrainingRequest(bhp.Id, new DateOnly(2021, 3, 1), null, null), HttpStatusCode.Created);
        var exam = await PostAsync<MedicalExamResponse>(
            hr, $"/api/v1/employees/{worker.Id}/medical-exams", new SaveMedicalExamRequest(MedicalExamKind.Initial, new DateOnly(2024, 3, 1), new DateOnly(2026, 3, 1), null), HttpStatusCode.Created);
        using var backwards = await hr.PostAsJsonAsync(
            new Uri($"/api/v1/employees/{worker.Id}/medical-exams", UriKind.Relative), new SaveMedicalExamRequest(MedicalExamKind.Periodic, new DateOnly(2025, 3, 1), new DateOnly(2025, 2, 1), null), Cancellation);
        var safety = await GetAsync<EmployeeSafetyResponse>(hr, $"/api/v1/employees/{worker.Id}/safety");
        var history = await hr.GetStringAsync(new Uri($"/api/v1/employees/{worker.Id}/history", UriKind.Relative), Cancellation);

        using var other = factory.CreateSignedInClient(await factory.CreateTenantAsync());
        using var foreign = await other.DeleteAsync(new Uri($"/api/v1/trainings/{training.Id}", UriKind.Relative), Cancellation);
        using var removed = await hr.DeleteAsync(new Uri($"/api/v1/medical-exams/{exam.Id}", UriKind.Relative), Cancellation);
        var otherCourses = await GetAsync<List<TrainingCourseResponse>>(other, "/api/v1/training-courses");

        // The validity of the course gives the end when HR types none; the newer training of a course is the current one.
        training.ValidUntil.ShouldBe(new DateOnly(2027, 3, 15));
        training.CourseName.ShouldBe("BHP okresowe");
        training.IsCurrent.ShouldBeTrue();
        older.IsCurrent.ShouldBeFalse();
        exam.IsCurrent.ShouldBeTrue();
        await ShouldFailAsync(taken, HttpStatusCode.Conflict, "training_course.name_taken");
        await ShouldFailAsync(tooLong, HttpStatusCode.BadRequest, "training_course.validity_out_of_range");
        await ShouldFailAsync(backwards, HttpStatusCode.BadRequest, "medical_exam.expires_before_date");
        safety.Trainings.Select(row => (row.CompletedOn, row.IsCurrent)).ShouldBe([(new DateOnly(2024, 3, 15), true), (new DateOnly(2021, 3, 1), false)]);
        safety.Exams.ShouldHaveSingleItem().Kind.ShouldBe(MedicalExamKind.Initial);

        // Cranes are mandatory in production, so also in assembly under it; Excel is neither mandatory nor for assembly.
        safety.MissingCourses.ShouldHaveSingleItem().Id.ShouldBe(cranes.Id);
        history.ShouldContain("BHP okresowe");
        history.ShouldContain("Badanie lekarskie");
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        otherCourses.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_report_lists_what_is_missing_has_expired_or_expires_soon_for_people_employed_today()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var bhp = await PostAsync<TrainingCourseResponse>(hr, "/api/v1/training-courses", new SaveTrainingCourseRequest("BHP okresowe", 12, true, [], null), HttpStatusCode.Created);
        var retired = await PostAsync<TrainingCourseResponse>(hr, "/api/v1/training-courses", new SaveTrainingCourseRequest("Stare szkolenie", 12, true, [], null), HttpStatusCode.Created);
        await PostAsync<TrainingCourseResponse>(hr, $"/api/v1/training-courses/{retired.Id}/deactivate", new { });
        var ala = await HireAsync(hr, "Ala", null);
        var olek = await HireAsync(hr, "Olek", null);

        // Ala: examination valid for long, training that ran out; Olek: examination running out soon, no training at all.
        await PostAsync<MedicalExamResponse>(hr, $"/api/v1/employees/{ala.Id}/medical-exams", new SaveMedicalExamRequest(MedicalExamKind.Periodic, Today.AddMonths(-1), Today.AddYears(2), null), HttpStatusCode.Created);
        await PostAsync<EmployeeTrainingResponse>(hr, $"/api/v1/employees/{ala.Id}/trainings", new SaveEmployeeTrainingRequest(bhp.Id, Today.AddYears(-2), null, null), HttpStatusCode.Created);
        await PostAsync<MedicalExamResponse>(hr, $"/api/v1/employees/{olek.Id}/medical-exams", new SaveMedicalExamRequest(MedicalExamKind.Periodic, Today.AddYears(-2), Today.AddDays(10), null), HttpStatusCode.Created);

        var report = await GetAsync<SafetyRenewalReport>(hr, "/api/v1/reports/safety-renewals?days=30");
        using var tooLong = await hr.GetAsync(new Uri("/api/v1/reports/safety-renewals?days=400", UriKind.Relative), Cancellation);

        report.From.ShouldBe(Today);
        report.To.ShouldBe(Today.AddDays(29));
        report.Rows.Select(row => (row.FirstName, row.Item, row.State)).ShouldBe(
        [
            ("Ala", SafetyItemKind.Training, SafetyRenewalState.Expired),
            ("Olek", SafetyItemKind.MedicalExam, SafetyRenewalState.Expiring),
            ("Olek", SafetyItemKind.Training, SafetyRenewalState.Missing),
        ]);
        report.Rows[0].ValidUntil.ShouldBe(Today.AddYears(-1));
        report.Rows[2].Name.ShouldBe("BHP okresowe");
        await ShouldFailAsync(tooLong, HttpStatusCode.BadRequest, "report.days_out_of_range");
    }

    [Fact]
    public async Task Trainings_and_examinations_of_the_earlier_system_are_imported_once()
    {
        var tenantId = await factory.CreateTenantAsync();
        using var hr = factory.CreateSignedInClient(tenantId);
        var production = await PostAsync<DepartmentResponse>(hr, "/api/v1/organization/departments", new CreateDepartmentRequest("Produkcja", "D7", null), HttpStatusCode.Created);
        var worker = await HireAsync(hr, "Marek", null);
        var request = new ImportSafetyRequest(
            [
                new ImportTrainingCourseRow(1, "BHP", 12, true, [], null),
                new ImportTrainingCourseRow(2, "Suwnice", 120, true, ["d7", "D99"], "UDT"),
                new ImportTrainingCourseRow(3, "Zła", 0, false, [], null),
            ],
            [
                new ImportTrainingRow(4, worker.Number.ToLowerInvariant(), "bhp", new DateOnly(2024, 5, 6), new DateOnly(2025, 5, 6), null),
                new ImportTrainingRow(5, worker.Number, "Suwnice", new DateOnly(2020, 1, 10), null, null),
                new ImportTrainingRow(6, worker.Number, "Nie ma takiego", new DateOnly(2020, 1, 10), null, null),
                new ImportTrainingRow(7, "NIEMA", "BHP", new DateOnly(2020, 1, 10), null, null),
            ],
            [
                new ImportMedicalExamRow(8, worker.Number, MedicalExamKind.Periodic, new DateOnly(2023, 2, 1), new DateOnly(2025, 2, 1), "skierowanie 5/2023"),
                new ImportMedicalExamRow(9, worker.Number, MedicalExamKind.Periodic, new DateOnly(2023, 9, 1), new DateOnly(2023, 8, 1), null),
            ]);

        var first = await PostAsync<ImportSafetyResponse>(hr, "/api/v1/safety/import", request);
        var again = await PostAsync<ImportSafetyResponse>(hr, "/api/v1/safety/import", request);
        var courses = await GetAsync<List<TrainingCourseResponse>>(hr, "/api/v1/training-courses");
        var safety = await GetAsync<EmployeeSafetyResponse>(hr, $"/api/v1/employees/{worker.Id}/safety");

        (first.CoursesAdded, first.TrainingsAdded, first.ExamsAdded, first.Existing).ShouldBe((2, 2, 1, 0));
        first.Problems.Select(problem => problem.Line).ShouldBe([3, 6, 7, 9]);
        (again.CoursesAdded, again.TrainingsAdded, again.ExamsAdded, again.Existing).ShouldBe((0, 0, 0, 5));
        courses.Single(course => course.Name == "Suwnice").DepartmentIds.ShouldBe([production.Id]);
        safety.Trainings.Single(training => training.CourseName == "Suwnice").ValidUntil.ShouldBe(new DateOnly(2030, 1, 10));
        safety.Exams.ShouldHaveSingleItem().Note.ShouldBe("skierowanie 5/2023");
    }

    private static async Task<EmployeeResponse> HireAsync(HttpClient client, string firstName, Guid? departmentId)
    {
        var number = "B" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        return await PostAsync<EmployeeResponse>(
            client, "/api/v1/employees", new SaveEmployeeRequest(number, firstName, "Bezpieczny", null, null, new DateOnly(2020, 1, 2), departmentId, null, null), HttpStatusCode.Created);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string uri) =>
        (await client.GetFromJsonAsync<T>(new Uri(uri, UriKind.Relative), Cancellation))!;

    private static async Task<T> PostAsync<T>(HttpClient client, string uri, object body, HttpStatusCode expected = HttpStatusCode.OK)
    {
        using var response = await client.PostAsJsonAsync(new Uri(uri, UriKind.Relative), body, Cancellation);
        response.StatusCode.ShouldBe(expected, await response.Content.ReadAsStringAsync(Cancellation));
        return (await response.Content.ReadFromJsonAsync<T>(Cancellation))!;
    }

    private static async Task ShouldFailAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        response.StatusCode.ShouldBe(status, body);
        body.ShouldContain(code);
    }
}
