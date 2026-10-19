using PersoNet.Contracts.Reports;
using PersoNet.Contracts.Safety;

namespace PersoNet.Desktop.Snapshots;

/// <summary>
/// The trainings a small company runs, and the certificates of one employee: a periodic examination running out soon,
/// a training that has expired and a mandatory one never done.
/// </summary>
internal sealed partial class DemoApi
{
    private List<TrainingCourseResponse>? _courses;
    private EmployeeSafetyResponse? _safety;

    private List<TrainingCourseResponse> Courses => _courses ??=
    [
        new(Guid.NewGuid(), "BHP okresowe – pracownicy administracyjno-biurowi", 72, true, [], "Prowadzi służba BHP; zaświadczenie do akt osobowych, część B.", true),
        new(Guid.NewGuid(), "BHP wstępne – instruktaż ogólny i stanowiskowy", null, true, [], null, true),
        new(Guid.NewGuid(), "Ochrona danych osobowych (RODO)", 12, true, [DepartmentId("KAD")], null, true),
        new(Guid.NewGuid(), "Obsługa wózków jezdniowych z napędem silnikowym", 60, true, [DepartmentId("SPR"), DepartmentId("IT-INF")], "Uprawnienia UDT; badania kierowcy wózka osobno.", true),
        new(Guid.NewGuid(), "Pierwsza pomoc przedmedyczna", 24, false, [], null, true),
        new(Guid.NewGuid(), "Obsługa kas fiskalnych", null, false, [], null, false),
    ];

    public Task<IReadOnlyList<TrainingCourseResponse>> GetCoursesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TrainingCourseResponse>>(
            Courses.Where(course => includeInactive || course.IsActive).OrderBy(course => course.Name, StringComparer.CurrentCulture).ToList());

    public Task<TrainingCourseResponse> CreateCourseAsync(SaveTrainingCourseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<TrainingCourseResponse> UpdateCourseAsync(Guid id, SaveTrainingCourseRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task SetCourseActiveAsync(Guid id, bool active, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EmployeeSafetyResponse> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        _safety ??= new EmployeeSafetyResponse(
            [
                new(Guid.NewGuid(), employeeId, MedicalExamKind.Periodic, today.AddYears(-2).AddDays(18), today.AddDays(18), "Bez przeciwwskazań do pracy przy monitorze.", true),
                new(Guid.NewGuid(), employeeId, MedicalExamKind.Initial, today.AddYears(-5).AddMonths(-2), today.AddYears(-2).AddDays(18), null, false),
            ],
            [
                new(Guid.NewGuid(), employeeId, Courses[0].Id, Courses[0].Name, today.AddYears(-3), today.AddYears(3), "Zaświadczenie nr 112/BHP/2023", true),
                new(Guid.NewGuid(), employeeId, Courses[2].Id, Courses[2].Name, today.AddMonths(-14), today.AddMonths(-2), null, true),
                new(Guid.NewGuid(), employeeId, Courses[1].Id, Courses[1].Name, today.AddYears(-5).AddMonths(-2), null, null, true),
            ],
            [Courses[3]]);
        return Task.FromResult(_safety);
    }

    public Task<MedicalExamResponse> CreateExamAsync(Guid employeeId, SaveMedicalExamRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<MedicalExamResponse> UpdateExamAsync(Guid id, SaveMedicalExamRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task DeleteExamAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<EmployeeTrainingResponse> CreateTrainingAsync(Guid employeeId, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task<EmployeeTrainingResponse> UpdateTrainingAsync(Guid id, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();

    public Task DeleteTrainingAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ImportSafetyResponse> ImportAsync(ImportSafetyRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new ImportSafetyResponse(request.Courses.Count, request.Trainings.Count, request.Exams.Count, 0, []));

    public Task<SafetyRenewalReport> GetSafetyRenewalsAsync(int days, CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var employed = _employees.Where(employee => employee.TerminationDate is null).OrderBy(employee => employee.Number, StringComparer.Ordinal).ToList();
        var rows = new List<SafetyRenewalRow>();
        for (var index = 0; index < employed.Count; index++)
        {
            var employee = employed[index];
            SafetyRenewalRow Row(SafetyItemKind item, string? name, DateOnly? done, DateOnly? until, SafetyRenewalState state) =>
                new(employee.Id, employee.Number, employee.FirstName, employee.LastName, DepartmentName(employee.DepartmentId), item, name, done, until, state);

            switch (index % 4)
            {
                case 0:
                    rows.Add(Row(SafetyItemKind.MedicalExam, null, today.AddYears(-3).AddDays(index), today.AddDays(index * 3), SafetyRenewalState.Expiring));
                    break;
                case 1:
                    rows.Add(Row(SafetyItemKind.Training, Courses[2].Name, today.AddYears(-1).AddDays(-index * 5), today.AddDays(-index * 5), SafetyRenewalState.Expired));
                    break;
                case 2 when employee.HireDate > today.AddYears(-1):
                    rows.Add(Row(SafetyItemKind.MedicalExam, null, null, null, SafetyRenewalState.Missing));
                    break;
                case 3:
                    rows.Add(Row(SafetyItemKind.Training, Courses[3].Name, null, null, SafetyRenewalState.Missing));
                    break;
            }
        }

        var order = rows.OrderBy(row => row.State switch { SafetyRenewalState.Expired => 0, SafetyRenewalState.Expiring => 1, _ => 2 })
            .ThenBy(row => row.ValidUntil)
            .ThenBy(row => row.LastName, StringComparer.CurrentCulture)
            .ToList();
        return Task.FromResult(new SafetyRenewalReport(today, today.AddDays(days), order));
    }

    private Guid DepartmentId(string code) => _departments.Single(department => department.Code == code).Id;
}
