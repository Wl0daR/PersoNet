using System.Net;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Absences;
using PersoNet.Contracts.Absences;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>In-memory stand-in for the absences API, which refuses an absence overlapping another one as the server does.</summary>
internal sealed class FakeAbsencesApi : IAbsencesApi
{
    public List<AbsenceResponse> Absences { get; } = [];

    public List<ImportLeaveHistoryRequest> Imported { get; } = [];

    public Task<IReadOnlyList<AbsenceResponse>> ListAsync(Guid employeeId, int? year, CancellationToken cancellationToken)
    {
        IReadOnlyList<AbsenceResponse> found = Absences
            .Where(absence => absence.EmployeeId == employeeId && (year is null || (absence.StartDate.Year <= year && absence.EndDate.Year >= year)))
            .OrderByDescending(absence => absence.StartDate)
            .ToList();
        return Task.FromResult(found);
    }

    public Task<AbsenceResponse> CreateAsync(Guid employeeId, SaveAbsenceRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(Save(Guid.NewGuid(), employeeId, request));

    public Task<AbsenceResponse> UpdateAsync(Guid id, SaveAbsenceRequest request, CancellationToken cancellationToken)
    {
        var current = Absences.Single(absence => absence.Id == id);
        Absences.Remove(current);
        return Task.FromResult(Save(id, current.EmployeeId, request));
    }

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        Absences.RemoveAll(absence => absence.Id == id);
        return Task.CompletedTask;
    }

    public Task<ImportLeaveHistoryResponse> ImportHistoryAsync(ImportLeaveHistoryRequest request, CancellationToken cancellationToken)
    {
        Imported.Add(request);
        return Task.FromResult(new ImportLeaveHistoryResponse(request.Leave.Count, request.Absences.Count, request.Entitlements.Count, 0, []));
    }

    private AbsenceResponse Save(Guid id, Guid employeeId, SaveAbsenceRequest request)
    {
        if (Absences.Any(other => other.EmployeeId == employeeId && other.Id != id && other.StartDate <= request.EndDate && request.StartDate <= other.EndDate))
        {
            throw new ApiException(HttpStatusCode.Conflict, "W tym okresie pracownik ma już wpisaną inną nieobecność.", "absence.overlap");
        }

        var days = request.EndDate.DayNumber - request.StartDate.DayNumber + 1;
        var absence = new AbsenceResponse(id, employeeId, request.Kind, request.StartDate, request.EndDate, days, days, request.Note);
        Absences.Add(absence);
        return absence;
    }
}
