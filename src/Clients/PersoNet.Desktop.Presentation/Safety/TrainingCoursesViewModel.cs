using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.ApiClient.Safety;
using PersoNet.Contracts.Organization;
using PersoNet.Contracts.Safety;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Safety;

/// <summary>
/// "Szkolenia" screen: the company's list of trainings with how often each is repeated and for whom it is mandatory.
/// A new or changed training is filled in a dialog.
/// </summary>
public sealed partial class TrainingCoursesViewModel(ISafetyApi api, IOrganizationApi organization, IDialogService dialogs) : EditorViewModel
{
    private IReadOnlyList<DepartmentResponse> _departments = [];

    public ObservableCollection<TrainingCourseRow> Courses { get; } = [];

    /// <summary>True when there is nothing to show in the list, so the view can explain what to do instead.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial bool ShowInactive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleActiveText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand), nameof(EditCommand))]
    public partial TrainingCourseRow? Selected { get; set; }

    public string ToggleActiveText => Selected?.IsActive == false ? "Przywróć" : "Nie prowadzimy";

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(() => ReloadSelectingAsync(Selected?.Id, cancellationToken));

    [RelayCommand]
    public Task NewAsync(CancellationToken cancellationToken) =>
        EditInDialogAsync(new TrainingCourseDialogViewModel(api, _departments, editing: null), cancellationToken);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    public Task EditAsync(TrainingCourseRow? course, CancellationToken cancellationToken) => (course ?? Selected) is not { } current
        ? Task.CompletedTask
        : EditInDialogAsync(new TrainingCourseDialogViewModel(api, _departments, current.Course), cancellationToken);

    [RelayCommand(CanExecute = nameof(CanToggleActive))]
    public Task ToggleActiveAsync(CancellationToken cancellationToken) => Selected is not { } current
        ? Task.CompletedTask
        : RunAsync(async () =>
        {
            await api.SetCourseActiveAsync(current.Id, !current.IsActive, cancellationToken);
            await ReloadSelectingAsync(current.Id, cancellationToken);
        });

    protected override bool TryShowOnField(ApiException exception) => false;

    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync(CancellationToken.None);

    private async Task EditInDialogAsync(TrainingCourseDialogViewModel dialog, CancellationToken cancellationToken)
    {
        if (await dialogs.ShowAsync(dialog))
        {
            await RunAsync(() => ReloadSelectingAsync(dialog.Saved?.Id, cancellationToken));
        }
    }

    private bool CanEdit(TrainingCourseRow? course) => (course ?? Selected) is not null;

    private bool CanToggleActive() => Selected is not null;

    private async Task ReloadSelectingAsync(Guid? id, CancellationToken cancellationToken)
    {
        var courses = await api.GetCoursesAsync(ShowInactive, cancellationToken);
        _departments = await organization.GetDepartmentsAsync(includeInactive: true, cancellationToken);
        var names = _departments.ToDictionary(department => department.Id, department => department.Name);

        Courses.Clear();
        foreach (var course in courses)
        {
            var departments = course.DepartmentIds.Select(id => names.GetValueOrDefault(id)).OfType<string>().Order(StringComparer.CurrentCulture).ToList();
            var forWhom = !course.IsMandatory ? "nieobowiązkowe" : departments.Count == 0 ? "wszyscy pracownicy" : string.Join(", ", departments);
            Courses.Add(new TrainingCourseRow(course, SafetyTexts.Validity(course.ValidityMonths), forWhom));
        }

        IsEmpty = Courses.Count == 0;
        Selected = Courses.FirstOrDefault(course => course.Id == id);
    }
}

/// <summary>A training in the list, with its validity and for whom it is mandatory ready to show.</summary>
public sealed record TrainingCourseRow(TrainingCourseResponse Course, string ValidityText, string MandatoryFor)
{
    public Guid Id => Course.Id;

    public string Name => Course.Name;

    public bool IsActive => Course.IsActive;

    public bool IsMandatory => Course.IsMandatory;
}
