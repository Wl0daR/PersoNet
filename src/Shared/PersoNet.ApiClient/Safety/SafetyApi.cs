using System.Net.Http.Json;
using PersoNet.Contracts.Safety;

namespace PersoNet.ApiClient.Safety;

internal sealed class SafetyApi(HttpClient http) : ISafetyApi
{
    private const string Courses = "api/v1/training-courses";
    private const string Exams = "api/v1/medical-exams";
    private const string Trainings = "api/v1/trainings";

    public async Task<IReadOnlyList<TrainingCourseResponse>> GetCoursesAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"{Courses}?includeInactive={(includeInactive ? "true" : "false")}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<List<TrainingCourseResponse>>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TrainingCourseResponse> CreateCourseAsync(SaveTrainingCourseRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync(Courses, request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<TrainingCourseResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<TrainingCourseResponse> UpdateCourseAsync(Guid id, SaveTrainingCourseRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"{Courses}/{id}", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<TrainingCourseResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task SetCourseActiveAsync(Guid id, bool active, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(new Uri($"{Courses}/{id}/{(active ? "activate" : "deactivate")}", UriKind.Relative), content: null, cancellationToken).ConfigureAwait(false);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeSafetyResponse> GetEmployeeAsync(Guid employeeId, CancellationToken cancellationToken)
    {
        using var response = await http.GetAsync(new Uri($"api/v1/employees/{employeeId}/safety", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeSafetyResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MedicalExamResponse> CreateExamAsync(Guid employeeId, SaveMedicalExamRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"api/v1/employees/{employeeId}/medical-exams", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<MedicalExamResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<MedicalExamResponse> UpdateExamAsync(Guid id, SaveMedicalExamRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"{Exams}/{id}", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<MedicalExamResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteExamAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(new Uri($"{Exams}/{id}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeTrainingResponse> CreateTrainingAsync(Guid employeeId, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync($"api/v1/employees/{employeeId}/trainings", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeTrainingResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<EmployeeTrainingResponse> UpdateTrainingAsync(Guid id, SaveEmployeeTrainingRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PutAsJsonAsync($"{Trainings}/{id}", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<EmployeeTrainingResponse>(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteTrainingAsync(Guid id, CancellationToken cancellationToken)
    {
        using var response = await http.DeleteAsync(new Uri($"{Trainings}/{id}", UriKind.Relative), cancellationToken).ConfigureAwait(false);
        await ApiResponses.EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);
    }

    public async Task<ImportSafetyResponse> ImportAsync(ImportSafetyRequest request, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/v1/safety/import", request, cancellationToken).ConfigureAwait(false);
        return await ApiResponses.ReadAsync<ImportSafetyResponse>(response, cancellationToken).ConfigureAwait(false);
    }
}
