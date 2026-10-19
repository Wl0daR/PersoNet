using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Safety;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Safety;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Safety;

/// <summary>A training of the list in the drop-down, with how often it is repeated, e.g. "BHP okresowe (co 3 lata)".</summary>
public sealed record TrainingCourseChoice(TrainingCourseResponse Course)
{
    public Guid Id => Course.Id;

    public string Name => Course.ValidityMonths is null ? Course.Name : $"{Course.Name} ({SafetyTexts.Validity(Course.ValidityMonths)})";
}

/// <summary>
/// Dialog with a training the employee completed. Picking the training and the day fills in the validity from the list of
/// trainings; HR can change it when the certificate says otherwise.
/// </summary>
public sealed partial class TrainingDialogViewModel : DialogViewModel
{
    public const int NoteMaxLength = 500;

    private readonly ISafetyApi _api;
    private readonly EmployeeResponse _employee;
    private readonly EmployeeTrainingResponse? _training;
    private readonly bool _ready;

    public TrainingDialogViewModel(
        ISafetyApi api, EmployeeResponse employee, IEnumerable<TrainingCourseResponse> courses, EmployeeTrainingResponse? training, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(employee);
        ArgumentNullException.ThrowIfNull(courses);

        _api = api;
        _employee = employee;
        _training = training;
        Courses = [.. courses.OrderBy(course => course.Name, StringComparer.CurrentCulture).Select(course => new TrainingCourseChoice(course))];
        EditCourse = Courses.FirstOrDefault(choice => choice.Id == training?.CourseId);
        EditCompletedOn = (training?.CompletedOn ?? today).ToDateTime(TimeOnly.MinValue);
        EditValidUntil = training?.ValidUntil?.ToDateTime(TimeOnly.MinValue);
        EditNote = training?.Note ?? string.Empty;
        _ready = true;
        ClearAllErrors();
    }

    public override string Title => IsNew ? $"Nowe szkolenie – {EmployeeName}" : $"Szkolenie – {EmployeeName}";

    public string EmployeeName => $"{_employee.FirstName} {_employee.LastName}";

    public bool IsNew => _training is null;

    public IReadOnlyList<TrainingCourseChoice> Courses { get; }

    /// <summary>True when the company's list of trainings is empty, so the dialog says where to fill it in.</summary>
    public bool HasNoCourses => Courses.Count == 0;

    /// <summary>The training after a successful save.</summary>
    public EmployeeTrainingResponse? Saved { get; private set; }

    [ObservableProperty]
    public partial TrainingCourseChoice? EditCourse { get; set; }

    [ObservableProperty]
    public partial DateTime? EditCompletedOn { get; set; }

    [ObservableProperty]
    public partial DateTime? EditValidUntil { get; set; }

    [ObservableProperty]
    public partial string EditNote { get; set; }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SetError(nameof(EditCourse), EditCourse is null ? "Wybierz szkolenie z listy." : null);
        SetError(nameof(EditCompletedOn), EditCompletedOn is null ? "Podaj datę ukończenia szkolenia." : null);
        SetError(nameof(EditValidUntil), EditCompletedOn is { } date && EditValidUntil < date ? "Szkolenie nie może tracić ważności przed dniem ukończenia." : null);
        if (HasErrors)
        {
            return Task.CompletedTask;
        }

        var request = new SaveEmployeeTrainingRequest(
            EditCourse!.Id,
            DateOnly.FromDateTime(EditCompletedOn!.Value),
            EditValidUntil is { } until ? DateOnly.FromDateTime(until) : null,
            string.IsNullOrWhiteSpace(EditNote) ? null : EditNote.Trim());

        return RunAsync(async () =>
        {
            Saved = _training is { } current
                ? await _api.UpdateTrainingAsync(current.Id, request, cancellationToken)
                : await _api.CreateTrainingAsync(_employee.Id, request, cancellationToken);

            HasChanges = true;
            Close(true);
        });
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "training.course_not_found" => nameof(EditCourse),
            "training.expires_before_date" => nameof(EditValidUntil),
            "safety.note_too_long" => nameof(EditNote),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }

    partial void OnEditCourseChanged(TrainingCourseChoice? value) => FillValidity();

    partial void OnEditCompletedOnChanged(DateTime? value) => FillValidity();

    // The validity follows the training and the day picked; one that does not expire has no end.
    private void FillValidity()
    {
        if (_ready && EditCourse is { } course && EditCompletedOn is { } completed)
        {
            EditValidUntil = course.Course.ValidityMonths is { } months ? completed.AddMonths(months) : null;
        }
    }
}
