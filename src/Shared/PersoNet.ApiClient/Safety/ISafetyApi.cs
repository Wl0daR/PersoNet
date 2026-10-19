using PersoNet.Contracts.Safety;

namespace PersoNet.ApiClient.Safety;

/// <summary>Medical examinations and trainings (BHP). Failures throw <see cref="ApiException"/>.</summary>
public interface ISafetyApi
{
    Task<IReadOnlyList<TrainingCourseResponse>> GetCoursesAsync(bool includeInactive, CancellationToken cancellationToken);

    Task<TrainingCourseResponse> CreateCourseAsync(SaveTrainingCourseRequest request, CancellationToken cancellationToken);

    Task<TrainingCourseResponse> UpdateCourseAsync(Guid id, SaveTrainingCourseRequest request, CancellationToken cancellationToken);

    Task SetCourseActiveAsync(Guid id, bool active, CancellationToken cancellationToken);

    Task<EmployeeSafetyResponse> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken);

    Task<MedicalExamResponse> CreateExamAsync(Guid employeeId, SaveMedicalExamRequest request, CancellationToken cancellationToken);

    Task<MedicalExamResponse> UpdateExamAsync(Guid id, SaveMedicalExamRequest request, CancellationToken cancellationToken);

    Task DeleteExamAsync(Guid id, CancellationToken cancellationToken);

    Task<EmployeeTrainingResponse> CreateTrainingAsync(Guid employeeId, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken);

    Task<EmployeeTrainingResponse> UpdateTrainingAsync(Guid id, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken);

    Task DeleteTrainingAsync(Guid id, CancellationToken cancellationToken);

    Task<ImportSafetyResponse> ImportAsync(ImportSafetyRequest request, CancellationToken cancellationToken);
}
