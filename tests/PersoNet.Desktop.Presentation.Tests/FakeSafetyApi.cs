using System.Net;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Safety;
using PersoNet.Contracts.Safety;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>Keeps trainings, examinations and the list of trainings in memory, with the rules of the API that the screens show.</summary>
internal sealed class FakeSafetyApi : ISafetyApi
{
    public List<TrainingCourseResponse> Courses { get; } = [];

    public List<MedicalExamResponse> Exams { get; } = [];

    public List<EmployeeTrainingResponse> Trainings { get; } = [];

    public List<ImportSafetyRequest> Imported { get; } = [];

    public TrainingCourseResponse AddCourse(string name, int? validityMonths, bool isMandatory = true, IReadOnlyList<Guid>? departmentIds = null)
    {
        var course = new TrainingCourseResponse(Guid.NewGuid(), name, validityMonths, isMandatory, departmentIds ?? [], null, true);
        Courses.Add(course);
        return course;
    }

    public Task<IReadOnlyList<TrainingCourseResponse>> GetCoursesAsync(bool includeInactive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<TrainingCourseResponse>>([.. Courses.Where(course => includeInactive || course.IsActive).OrderBy(course => course.Name, StringComparer.CurrentCulture)]);

    public Task<TrainingCourseResponse> CreateCourseAsync(SaveTrainingCourseRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(SaveCourse(Guid.NewGuid(), request, isActive: true));

    public Task<TrainingCourseResponse> UpdateCourseAsync(Guid id, SaveTrainingCourseRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(SaveCourse(id, request, Courses.Single(course => course.Id == id).IsActive));

    public Task SetCourseActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        var index = Courses.FindIndex(course => course.Id == id);
        Courses[index] = Courses[index] with { IsActive = active };
        return Task.CompletedTask;
    }

    public Task<EmployeeSafetyResponse> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        var exams = Exams.Where(exam => exam.EmployeeId == employeeId).OrderByDescending(exam => exam.ExamDate).ToList();
        var trainings = Trainings.Where(training => training.EmployeeId == employeeId).OrderByDescending(training => training.CompletedOn).ToList();
        var latest = trainings.GroupBy(training => training.CourseId).Select(group => group.First().Id).ToHashSet();
        var missing = Courses.Where(course => course.IsActive && course.IsMandatory && course.DepartmentIds.Count == 0 && trainings.All(training => training.CourseId != course.Id)).ToList();

        return Task.FromResult(new EmployeeSafetyResponse(
            [.. exams.Select((exam, index) => exam with { IsCurrent = index == 0 })],
            [.. trainings.Select(training => training with { IsCurrent = latest.Contains(training.Id) })],
            missing));
    }

    public Task<MedicalExamResponse> CreateExamAsync(Guid employeeId, SaveMedicalExamRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(SaveExam(Guid.NewGuid(), employeeId, request));

    public Task<MedicalExamResponse> UpdateExamAsync(Guid id, SaveMedicalExamRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(SaveExam(id, Exams.Single(exam => exam.Id == id).EmployeeId, request));

    public Task DeleteExamAsync(Guid id, CancellationToken cancellationToken)
    {
        Exams.RemoveAll(exam => exam.Id == id);
        return Task.CompletedTask;
    }

    public Task<EmployeeTrainingResponse> CreateTrainingAsync(Guid employeeId, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(SaveTraining(Guid.NewGuid(), employeeId, request));

    public Task<EmployeeTrainingResponse> UpdateTrainingAsync(Guid id, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(SaveTraining(id, Trainings.Single(training => training.Id == id).EmployeeId, request));

    public Task DeleteTrainingAsync(Guid id, CancellationToken cancellationToken)
    {
        Trainings.RemoveAll(training => training.Id == id);
        return Task.CompletedTask;
    }

    public Task<ImportSafetyResponse> ImportAsync(ImportSafetyRequest request, CancellationToken cancellationToken)
    {
        Imported.Add(request);
        return Task.FromResult(new ImportSafetyResponse(request.Courses.Count, request.Trainings.Count, request.Exams.Count, 0, []));
    }

    private TrainingCourseResponse SaveCourse(Guid id, SaveTrainingCourseRequest request, bool isActive)
    {
        if (Courses.Any(course => course.Id != id && string.Equals(course.Name, request.Name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            throw new ApiException(HttpStatusCode.Conflict, "Szkolenie o tej nazwie już jest na liście.", "training_course.name_taken");
        }

        var course = new TrainingCourseResponse(id, request.Name.Trim(), request.ValidityMonths, request.IsMandatory, request.DepartmentIds ?? [], request.Description, isActive);
        Courses.RemoveAll(other => other.Id == id);
        Courses.Add(course);
        return course;
    }

    private MedicalExamResponse SaveExam(Guid id, Guid employeeId, SaveMedicalExamRequest request)
    {
        var exam = new MedicalExamResponse(id, employeeId, request.Kind, request.ExamDate, request.ValidUntil, request.Note, true);
        Exams.RemoveAll(other => other.Id == id);
        Exams.Add(exam);
        return exam;
    }

    private EmployeeTrainingResponse SaveTraining(Guid id, Guid employeeId, SaveEmployeeTrainingRequest request)
    {
        var course = Courses.Single(course => course.Id == request.CourseId);
        var validUntil = request.ValidUntil ?? (course.ValidityMonths is { } months ? request.CompletedOn.AddMonths(months) : null);
        var training = new EmployeeTrainingResponse(id, employeeId, course.Id, course.Name, request.CompletedOn, validUntil, request.Note, true);
        Trainings.RemoveAll(other => other.Id == id);
        Trainings.Add(training);
        return training;
    }
}
