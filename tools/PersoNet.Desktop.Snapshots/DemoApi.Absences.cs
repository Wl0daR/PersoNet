using PersoNet.Contracts.Absences;

namespace PersoNet.Desktop.Snapshots;

/// <summary>A year of absences HR typically records: a sick note, care of a child, a business trip, training and remote work.</summary>
internal sealed partial class DemoApi
{
    private List<AbsenceResponse>? _absences;

    public Task<IReadOnlyList<AbsenceResponse>> ListAsync(Guid employeeId, int? year, CancellationToken cancellationToken)
    {
        _absences ??= DemoAbsences(FirstEmployeeWithContracts, DateTime.Today.Year);
        IReadOnlyList<AbsenceResponse> found = _absences
            .Where(absence => absence.EmployeeId == employeeId && (year is null || (absence.StartDate.Year <= year && absence.EndDate.Year >= year)))
            .OrderByDescending(absence => absence.StartDate)
            .ToList();
        return Task.FromResult(found);
    }

    public Task<AbsenceResponse> CreateAsync(Guid employeeId, SaveAbsenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<AbsenceResponse> UpdateAsync(Guid id, SaveAbsenceRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task<ImportLeaveHistoryResponse> ImportHistoryAsync(ImportLeaveHistoryRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();

    private static List<AbsenceResponse> DemoAbsences(Guid employeeId, int year)
    {
        AbsenceResponse Absence(AbsenceKind kind, int month, int day, int days, string? note)
        {
            var start = new DateOnly(year, month, day);
            var end = start.AddDays(days - 1);
            var working = Enumerable.Range(0, days).Count(offset => start.AddDays(offset).DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
            return new AbsenceResponse(Guid.NewGuid(), employeeId, kind, start, end, days, working, note);
        }

        return
        [
            Absence(AbsenceKind.Sickness, 2, 3, 10, "e-ZLA seria ZY 1234567"),
            Absence(AbsenceKind.ChildCareAllowance, 3, 17, 2, null),
            Absence(AbsenceKind.BusinessTrip, 4, 22, 3, "Targi Budma, Poznań – stoisko firmy"),
            Absence(AbsenceKind.Training, 5, 14, 1, "Szkolenie BHP okresowe"),
            Absence(AbsenceKind.RemoteWork, 6, 2, 5, null),
            Absence(AbsenceKind.MedicalExam, 6, 9, 1, "Badania okresowe"),
        ];
    }
}
