using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Safety;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Safety;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Safety;

/// <summary>A kind of medical examination with its Polish name, for the drop-down list.</summary>
public sealed record MedicalExamKindChoice(MedicalExamKind Kind, string Name);

/// <summary>Dialog with a new medical examination or the one being changed, as the doctor's certificate says.</summary>
public sealed partial class MedicalExamDialogViewModel : DialogViewModel
{
    public const int NoteMaxLength = 500;

    private readonly ISafetyApi _api;
    private readonly EmployeeResponse _employee;
    private readonly MedicalExamResponse? _exam;

    public MedicalExamDialogViewModel(ISafetyApi api, EmployeeResponse employee, MedicalExamResponse? exam, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(employee);

        _api = api;
        _employee = employee;
        _exam = exam;

        // A new one is usually the periodic examination of today, valid as the doctor writes.
        EditKind = Kinds.First(kind => kind.Kind == (exam?.Kind ?? MedicalExamKind.Periodic));
        EditExamDate = (exam?.ExamDate ?? today).ToDateTime(TimeOnly.MinValue);
        EditValidUntil = exam?.ValidUntil?.ToDateTime(TimeOnly.MinValue);
        EditNote = exam?.Note ?? string.Empty;
        ClearAllErrors();
    }

    public static IReadOnlyList<MedicalExamKindChoice> Kinds { get; } =
    [
        new(MedicalExamKind.Initial, SafetyTexts.Name(MedicalExamKind.Initial)),
        new(MedicalExamKind.Periodic, SafetyTexts.Name(MedicalExamKind.Periodic)),
        new(MedicalExamKind.Control, SafetyTexts.Name(MedicalExamKind.Control)),
    ];

    public override string Title => IsNew ? $"Nowe badanie lekarskie – {EmployeeName}" : $"Badanie lekarskie – {EmployeeName}";

    public string EmployeeName => $"{_employee.FirstName} {_employee.LastName}";

    public bool IsNew => _exam is null;

    /// <summary>The examination after a successful save.</summary>
    public MedicalExamResponse? Saved { get; private set; }

    [ObservableProperty]
    public partial MedicalExamKindChoice EditKind { get; set; }

    [ObservableProperty]
    public partial DateTime? EditExamDate { get; set; }

    [ObservableProperty]
    public partial DateTime? EditValidUntil { get; set; }

    [ObservableProperty]
    public partial string EditNote { get; set; }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SetError(nameof(EditExamDate), EditExamDate is null ? "Podaj datę badania." : null);
        SetError(nameof(EditValidUntil), EditExamDate is { } date && EditValidUntil < date ? "Orzeczenie nie może tracić ważności przed dniem badania." : null);
        if (HasErrors)
        {
            return Task.CompletedTask;
        }

        var request = new SaveMedicalExamRequest(
            EditKind.Kind,
            DateOnly.FromDateTime(EditExamDate!.Value),
            EditValidUntil is { } until ? DateOnly.FromDateTime(until) : null,
            string.IsNullOrWhiteSpace(EditNote) ? null : EditNote.Trim());

        return RunAsync(async () =>
        {
            Saved = _exam is { } current
                ? await _api.UpdateExamAsync(current.Id, request, cancellationToken)
                : await _api.CreateExamAsync(_employee.Id, request, cancellationToken);

            HasChanges = true;
            Close(true);
        });
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "medical_exam.expires_before_date" => nameof(EditValidUntil),
            "safety.note_too_long" => nameof(EditNote),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }
}
