using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Safety;
using PersoNet.Contracts.Organization;
using PersoNet.Contracts.Safety;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Organization;

namespace PersoNet.Desktop.Presentation.Safety;

/// <summary>
/// Dialog with a training of the company's list: its name, how often it is repeated and, for a mandatory one, the
/// departments it concerns (with the ones under them).
/// </summary>
public sealed partial class TrainingCourseDialogViewModel : DialogViewModel
{
    public const int NameMaxLength = 200;

    public const int DescriptionMaxLength = 1000;

    public const int MaxValidityMonths = 240;

    private readonly ISafetyApi _api;
    private readonly TrainingCourseResponse? _editing;

    public TrainingCourseDialogViewModel(ISafetyApi api, IEnumerable<DepartmentResponse> departments, TrainingCourseResponse? editing)
    {
        ArgumentNullException.ThrowIfNull(departments);

        _api = api;
        _editing = editing;
        EditName = editing?.Name ?? string.Empty;
        EditExpires = editing is null || editing.ValidityMonths is not null;
        EditValidityMonths = (editing?.ValidityMonths ?? 12).ToString(CultureInfo.CurrentCulture);
        EditMandatory = editing?.IsMandatory ?? true;
        EditDescription = editing?.Description ?? string.Empty;

        // The tree in order, each department indented under its parent; an inactive one only while the training has it.
        var assigned = editing?.DepartmentIds.ToHashSet() ?? [];
        Departments = [.. DepartmentNode.BuildTree(departments)
            .SelectMany(root => Flatten(root, depth: 0))
            .Where(option => option.Node.Department.IsActive || assigned.Contains(option.Node.Id))
            .Select(option => new DepartmentOption(option.Node, option.Depth, assigned.Contains(option.Node.Id)))];
        foreach (var option in Departments)
        {
            option.PropertyChanged += (_, _) => OnPropertyChanged(nameof(DepartmentsSummary));
        }

        ClearAllErrors();
    }

    public bool IsNew => _editing is null;

    public override string Title => IsNew ? "Nowe szkolenie" : $"Szkolenie – {_editing!.Name}";

    /// <summary>The training after a successful save.</summary>
    public TrainingCourseResponse? Saved { get; private set; }

    [ObservableProperty]
    public partial string EditName { get; set; }

    /// <summary>True when the training has to be repeated after <see cref="EditValidityMonths"/>.</summary>
    [ObservableProperty]
    public partial bool EditExpires { get; set; }

    [ObservableProperty]
    public partial string EditValidityMonths { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DepartmentsSummary))]
    public partial bool EditMandatory { get; set; }

    [ObservableProperty]
    public partial string EditDescription { get; set; }

    /// <summary>All departments with a check box each, in tree order.</summary>
    public ObservableCollection<DepartmentOption> Departments { get; }

    public bool HasDepartments => Departments.Count > 0;

    /// <summary>Narrows the department list by name or code; checked departments stay checked while hidden.</summary>
    [ObservableProperty]
    public partial string DepartmentFilter { get; set; } = string.Empty;

    public IReadOnlyList<Guid> CheckedDepartmentIds => Departments.Where(option => option.IsChecked).Select(option => option.Id).ToList();

    public string DepartmentsSummary => !EditMandatory
        ? "Szkolenie nieobowiązkowe: nikt nie będzie wykazany jako bez niego."
        : CheckedDepartmentIds.Count switch
        {
            0 => "Nie wybrano działu: szkolenie jest obowiązkowe dla wszystkich pracowników.",
            1 => "Obowiązkowe w 1 dziale i działach mu podległych.",
            var count => $"Obowiązkowe w wybranych działach ({count}) i działach im podległych.",
        };

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        int? months = null;
        SetError(nameof(EditName), string.IsNullOrWhiteSpace(EditName) ? "Podaj nazwę szkolenia." : null);
        if (EditExpires)
        {
            months = int.TryParse(EditValidityMonths.Trim(), NumberStyles.Integer, CultureInfo.CurrentCulture, out var value) && value is >= 1 and <= MaxValidityMonths ? value : null;
            SetError(nameof(EditValidityMonths), months is null ? $"Podaj liczbę miesięcy od 1 do {MaxValidityMonths}, np. 12 dla szkolenia co rok." : null);
        }
        else
        {
            SetError(nameof(EditValidityMonths), null);
        }

        if (HasErrors)
        {
            return Task.CompletedTask;
        }

        var request = new SaveTrainingCourseRequest(
            EditName.Trim(),
            months,
            EditMandatory,
            EditMandatory ? CheckedDepartmentIds : [],
            string.IsNullOrWhiteSpace(EditDescription) ? null : EditDescription.Trim());

        return RunAsync(async () =>
        {
            Saved = _editing is { } current
                ? await _api.UpdateCourseAsync(current.Id, request, cancellationToken)
                : await _api.CreateCourseAsync(request, cancellationToken);

            HasChanges = true;
            Close(true);
        });
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "training_course.name_taken" or "training_course.name_required" => nameof(EditName),
            "training_course.validity_out_of_range" => nameof(EditValidityMonths),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }

    partial void OnDepartmentFilterChanged(string value)
    {
        var text = value.Trim();
        foreach (var option in Departments)
        {
            option.IsVisible = text.Length == 0
                || option.Name.Contains(text, StringComparison.CurrentCultureIgnoreCase)
                || option.Code.Contains(text, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    private static IEnumerable<(DepartmentNode Node, int Depth)> Flatten(DepartmentNode node, int depth) =>
        node.Children.SelectMany(child => Flatten(child, depth + 1)).Prepend((node, depth));
}
