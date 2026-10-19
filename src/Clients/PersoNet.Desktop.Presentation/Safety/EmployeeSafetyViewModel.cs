using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Safety;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Safety;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Safety;

/// <summary>A medical examination or a completed training, as one row of the card's list.</summary>
public sealed record SafetyRow(
    MedicalExamResponse? Exam,
    EmployeeTrainingResponse? Training,
    string Kind,
    string Name,
    string DateText,
    string ValidUntilText,
    string StateText,
    SafetyTone Tone,
    string? Note)
{
    public bool IsExam => Exam is not null;

    public Guid Id => Exam?.Id ?? Training!.Id;
}

/// <summary>
/// "Badania i szkolenia" tab of an employee's card: the occupational medical examinations and the completed trainings,
/// the latest first, with whether each is still valid, and the mandatory trainings the employee has not had. Each is
/// added or changed in a dialog; removal needs a second click.
/// </summary>
public sealed partial class EmployeeSafetyViewModel : EditorViewModel
{
    private readonly ISafetyApi _api;
    private readonly TimeProvider _time;
    private readonly IDialogService _dialogs;
    private EmployeeResponse? _employee;
    private int _loadVersion;

    public EmployeeSafetyViewModel(ISafetyApi api, TimeProvider time, IDialogService dialogs, bool canManage)
    {
        _api = api;
        _time = time;
        _dialogs = dialogs;
        CanManage = canManage;
    }

    /// <summary>False for someone who may only look: no adding, changing or removing.</summary>
    public bool CanManage { get; }

    public ObservableCollection<SafetyRow> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    /// <summary>The state of the latest medical certificate, e.g. "Orzeczenie lekarskie ważne do 1 mar 2026".</summary>
    [ObservableProperty]
    public partial string? ExamSummary { get; set; }

    [ObservableProperty]
    public partial SafetyTone ExamTone { get; set; }

    /// <summary>Mandatory trainings never done, e.g. "Brak szkoleń obowiązkowych: BHP okresowe, Suwnice"; null when none.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasMissing))]
    public partial string? MissingText { get; set; }

    public bool HasMissing => MissingText is not null;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(DeleteCommand), nameof(EditCommand))]
    public partial SafetyRow? Selected { get; set; }

    /// <summary>First click on "Usuń" asks for confirmation; the second one deletes.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteText))]
    public partial bool IsConfirmingDelete { get; set; }

    public string DeleteText => IsConfirmingDelete ? "Na pewno usunąć?" : "Usuń";

    private DateOnly Today => DateOnly.FromDateTime(_time.GetLocalNow().Date);

    /// <summary>Shows the examinations and trainings of the employee (nothing for a card that is not saved yet).</summary>
    public Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        return LoadAsync(cancellationToken);
    }

    [RelayCommand]
    public Task NewExamAsync(CancellationToken cancellationToken) => _employee is not { } employee || !CanManage
        ? Task.CompletedTask
        : EditInDialogAsync(new MedicalExamDialogViewModel(_api, employee, exam: null, Today), cancellationToken);

    [RelayCommand]
    public async Task NewTrainingAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee || !CanManage)
        {
            return;
        }

        IReadOnlyList<TrainingCourseResponse> courses = [];
        await RunAsync(async () => courses = await _api.GetCoursesAsync(includeInactive: false, cancellationToken));
        if (ErrorMessage is null)
        {
            await EditInDialogAsync(new TrainingDialogViewModel(_api, employee, courses, training: null, Today), cancellationToken);
        }
    }

    /// <summary>Opens the picked examination or training in its form.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    public async Task EditAsync(SafetyRow? row, CancellationToken cancellationToken)
    {
        if (_employee is not { } employee || (row ?? Selected) is not { } current || !CanManage)
        {
            return;
        }

        if (current.Exam is { } exam)
        {
            await EditInDialogAsync(new MedicalExamDialogViewModel(_api, employee, exam, Today), cancellationToken);
            return;
        }

        // A training of a course no longer run stays in the list, so its own course can still be picked.
        IReadOnlyList<TrainingCourseResponse> courses = [];
        await RunAsync(async () => courses = await _api.GetCoursesAsync(includeInactive: true, cancellationToken));
        if (ErrorMessage is null)
        {
            var offered = courses.Where(course => course.IsActive || course.Id == current.Training!.CourseId).ToList();
            await EditInDialogAsync(new TrainingDialogViewModel(_api, employee, offered, current.Training, Today), cancellationToken);
        }
    }

    [RelayCommand(CanExecute = nameof(CanDelete))]
    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (Selected is not { } current)
        {
            return Task.CompletedTask;
        }

        if (!IsConfirmingDelete)
        {
            IsConfirmingDelete = true;
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            if (current.IsExam)
            {
                await _api.DeleteExamAsync(current.Id, cancellationToken);
            }
            else
            {
                await _api.DeleteTrainingAsync(current.Id, cancellationToken);
            }

            await ReloadAsync(selectId: null, cancellationToken);
        });
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    partial void OnSelectedChanged(SafetyRow? value) => IsConfirmingDelete = false;

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var version = ++_loadVersion;
        Items.Clear();
        IsEmpty = false;
        ExamSummary = null;
        MissingText = null;
        Selected = null;

        if (_employee is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var safety = await _api.GetEmployeeAsync(_employee.Id, cancellationToken);

            // Another employee may have been picked while this one loaded.
            if (version == _loadVersion)
            {
                Fill(safety, selectId: null);
            }
        });
    }

    private async Task EditInDialogAsync(DialogViewModel dialog, CancellationToken cancellationToken)
    {
        if (await _dialogs.ShowAsync(dialog))
        {
            var saved = dialog switch
            {
                MedicalExamDialogViewModel exam => exam.Saved?.Id,
                TrainingDialogViewModel training => training.Saved?.Id,
                _ => null,
            };
            await RunAsync(() => ReloadAsync(saved, cancellationToken));
        }
    }

    private async Task ReloadAsync(Guid? selectId, CancellationToken cancellationToken)
    {
        if (_employee is { } employee)
        {
            ++_loadVersion;
            Fill(await _api.GetEmployeeAsync(employee.Id, cancellationToken), selectId);
        }
    }

    private bool CanEdit(SafetyRow? row) => CanManage && (row ?? Selected) is not null;

    private bool CanDelete() => CanManage && Selected is not null;

    private void Fill(EmployeeSafetyResponse safety, Guid? selectId)
    {
        var today = Today;
        Items.Clear();
        foreach (var exam in safety.Exams)
        {
            var (text, tone) = SafetyTexts.State(exam.ValidUntil, today, exam.IsCurrent);
            Items.Add(new SafetyRow(
                exam, null, "Badanie lekarskie", SafetyTexts.Name(exam.Kind), SafetyTexts.Date(exam.ExamDate),
                exam.ValidUntil is { } until ? SafetyTexts.Date(until) : "–", text, tone, exam.Note));
        }

        foreach (var training in safety.Trainings)
        {
            var (text, tone) = SafetyTexts.State(training.ValidUntil, today, training.IsCurrent);
            Items.Add(new SafetyRow(
                null, training, "Szkolenie", training.CourseName, SafetyTexts.Date(training.CompletedOn),
                training.ValidUntil is { } until ? SafetyTexts.Date(until) : "bezterminowo", text, tone, training.Note));
        }

        IsEmpty = Items.Count == 0;
        Selected = Items.FirstOrDefault(row => row.Id == selectId);

        // Without a valid certificate the employee may not work, so its state is the first thing the tab says.
        var latest = safety.Exams.Count > 0 ? safety.Exams[0] : null;
        (ExamSummary, ExamTone) = latest switch
        {
            null => ("Brak badań lekarskich: pracownik nie może pracować bez ważnego orzeczenia.", SafetyTone.Critical),
            { ValidUntil: null } => ($"Orzeczenie lekarskie z {SafetyTexts.Date(latest.ExamDate)} bez daty ważności.", SafetyTone.Ok),
            { ValidUntil: { } until } when until < today => ($"Orzeczenie lekarskie straciło ważność {SafetyTexts.Date(until)}.", SafetyTone.Critical),
            { ValidUntil: { } until } => ($"Orzeczenie lekarskie ważne do {SafetyTexts.Date(until)}.", SafetyTexts.State(until, today, isCurrent: true).Tone),
        };
        MissingText = safety.MissingCourses.Count == 0
            ? null
            : $"Brak szkoleń obowiązkowych: {string.Join(", ", safety.MissingCourses.Select(course => course.Name))}.";
    }
}
