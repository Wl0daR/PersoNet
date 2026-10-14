using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Organization;

/// <summary>
/// Dialog with the name of a new position or of the one being changed, and the departments it belongs to. HR picking a
/// department in an employee card then sees that department's positions first.
/// </summary>
public sealed partial class PositionDialogViewModel : DialogViewModel
{
    public const int NameMaxLength = 200;

    private readonly IOrganizationApi _api;
    private readonly PositionResponse? _editing;

    public PositionDialogViewModel(IOrganizationApi api, IEnumerable<DepartmentResponse> departments, PositionResponse? editing)
    {
        ArgumentNullException.ThrowIfNull(departments);

        _api = api;
        _editing = editing;
        EditName = editing?.Name ?? string.Empty;

        // The tree in order, each department indented under its parent; an inactive one only while the position has it.
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

    public override string Title => IsNew ? "Nowe stanowisko" : $"Stanowisko – {_editing!.Name}";

    /// <summary>The position after a successful save.</summary>
    public PositionResponse? Saved { get; private set; }

    [ObservableProperty]
    public partial string EditName { get; set; }

    /// <summary>All departments with a check box each, in tree order.</summary>
    public ObservableCollection<DepartmentOption> Departments { get; }

    public bool HasDepartments => Departments.Count > 0;

    /// <summary>Text typed to narrow the department list; checked departments stay checked while hidden.</summary>
    [ObservableProperty]
    public partial string DepartmentFilter { get; set; } = string.Empty;

    public IReadOnlyList<Guid> CheckedDepartmentIds => Departments.Where(option => option.IsChecked).Select(option => option.Id).ToList();

    public string DepartmentsSummary => CheckedDepartmentIds.Count switch
    {
        0 => "Nie wybrano działu: stanowisko będzie do wyboru w każdym dziale.",
        1 => "Wybrano 1 dział.",
        var count => $"Wybrane działy: {count}.",
    };

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => !Validate() ? Task.CompletedTask : RunAsync(async () =>
    {
        var departmentIds = CheckedDepartmentIds;
        Saved = _editing is { } current
            ? await _api.UpdatePositionAsync(current.Id, new UpdatePositionRequest(EditName, departmentIds), cancellationToken)
            : await _api.CreatePositionAsync(new CreatePositionRequest(EditName, departmentIds), cancellationToken);

        HasChanges = true;
        Close(true);
    });

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Code != "position.name_taken")
        {
            return false;
        }

        SetError(nameof(EditName), exception.Message);
        return true;
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

    private bool Validate()
    {
        SetError(nameof(EditName), string.IsNullOrWhiteSpace(EditName) ? "Podaj nazwę stanowiska." : null);

        return !HasErrors;
    }
}

/// <summary>A department with a check box in the position dialog, indented by its place in the tree.</summary>
public sealed partial class DepartmentOption(DepartmentNode node, int depth, bool isChecked) : ObservableObject
{
    internal DepartmentNode Node => node;

    public Guid Id => node.Id;

    public string Name => node.Department.Name;

    public string Code => node.Department.Code;

    public string DisplayName => node.DisplayName;

    /// <summary>Left indent of the check box, one step per level below the top.</summary>
    public double Indent => depth * 20;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;

    [ObservableProperty]
    public partial bool IsVisible { get; set; } = true;
}
