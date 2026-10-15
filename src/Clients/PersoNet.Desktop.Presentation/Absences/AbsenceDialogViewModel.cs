using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Absences;
using PersoNet.Contracts.Absences;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Absences;

/// <summary>Dialog with the form of a new absence or of the one being changed.</summary>
public sealed partial class AbsenceDialogViewModel : DialogViewModel
{
    private readonly IAbsencesApi _api;
    private readonly EmployeeResponse _employee;
    private readonly AbsenceRow? _row;

    public AbsenceDialogViewModel(IAbsencesApi api, EmployeeResponse employee, AbsenceRow? row, DateTime start)
    {
        ArgumentNullException.ThrowIfNull(employee);

        _api = api;
        _employee = employee;
        _row = row;

        EditStartDate = start;
        EditEndDate = start;
        if (row?.Absence is { } absence)
        {
            EditKind = EmployeeAbsencesViewModel.Kinds.First(kind => kind.Kind == absence.Kind);
            EditEndDate = absence.EndDate.ToDateTime(TimeOnly.MinValue);
            EditNote = absence.Note ?? string.Empty;
        }

        ClearAllErrors();
    }

    public override string Title => IsNew ? $"Nowa nieobecność – {EmployeeName}" : $"Nieobecność – {EmployeeName}";

    public string EmployeeName => $"{_employee.FirstName} {_employee.LastName}";

    public const int NoteMaxLength = 500;

    public bool IsNew => _row is null;

    /// <summary>The absence after a successful save.</summary>
    public AbsenceResponse? Saved { get; private set; }

    [ObservableProperty]
    public partial AbsenceKindChoice EditKind { get; set; } = EmployeeAbsencesViewModel.Kinds[0];

    [ObservableProperty]
    public partial DateTime? EditStartDate { get; set; }

    [ObservableProperty]
    public partial DateTime? EditEndDate { get; set; }

    [ObservableProperty]
    public partial string EditNote { get; set; } = string.Empty;

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SetError(nameof(EditStartDate), EditStartDate is null ? "Podaj pierwszy dzień nieobecności." : null);
        SetError(nameof(EditEndDate), EditEndDate is null
            ? "Podaj ostatni dzień nieobecności."
            : EditStartDate is { } first && EditEndDate < first ? "Ostatni dzień nie może być przed pierwszym." : null);
        if (HasErrors)
        {
            return Task.CompletedTask;
        }

        var request = new SaveAbsenceRequest(
            EditKind.Kind,
            DateOnly.FromDateTime(EditStartDate!.Value),
            DateOnly.FromDateTime(EditEndDate!.Value),
            string.IsNullOrWhiteSpace(EditNote) ? null : EditNote.Trim());

        return RunAsync(async () =>
        {
            Saved = _row is { } current
                ? await _api.UpdateAsync(current.Absence.Id, request, cancellationToken)
                : await _api.CreateAsync(_employee.Id, request, cancellationToken);

            HasChanges = true;
            Close(true);
        });
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "absence.overlap" => nameof(EditStartDate),
            "absence.end_before_start" => nameof(EditEndDate),
            "absence.note_too_long" => nameof(EditNote),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }

    // A one-day absence is the usual case, so the end follows a start moved past it.
    partial void OnEditStartDateChanged(DateTime? value)
    {
        if (value is { } start && (EditEndDate is null || EditEndDate < start))
        {
            EditEndDate = start;
        }
    }
}
