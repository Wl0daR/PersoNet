using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Organization;

/// <summary>Dialog with the form of a new department or of the one being changed.</summary>
public sealed partial class DepartmentDialogViewModel : DialogViewModel
{
    public const int NameMaxLength = 200;

    public const int CodeMaxLength = 20;

    public static readonly ParentOption NoParent = new(null, "(brak – dział główny)");

    private readonly IOrganizationApi _api;
    private readonly DepartmentNode? _editing;

    /// <param name="api">Organization API that saves the department.</param>
    /// <param name="tree">All departments shown on the screen, for the parent list.</param>
    /// <param name="editing">The department being changed, or null for a new one.</param>
    /// <param name="parentId">Proposed parent of a new department.</param>
    public DepartmentDialogViewModel(IOrganizationApi api, IEnumerable<DepartmentNode> tree, DepartmentNode? editing, Guid? parentId)
    {
        ArgumentNullException.ThrowIfNull(tree);

        _api = api;
        _editing = editing;

        // A department cannot be moved under itself or its own sub-departments, so those are not offered.
        var excluded = editing?.SelfAndDescendants().Select(node => node.Id).ToHashSet() ?? [];
        foreach (var node in tree.SelectMany(root => root.SelfAndDescendants()).Where(node => !excluded.Contains(node.Id)))
        {
            ParentOptions.Add(new ParentOption(node.Id, node.DisplayName));
        }

        EditName = editing?.Department.Name ?? string.Empty;
        EditCode = editing?.Department.Code ?? string.Empty;
        EditParent = ParentOptions.FirstOrDefault(option => option.Id == (editing?.Department.ParentId ?? parentId)) ?? NoParent;
        ClearAllErrors();
    }

    public ObservableCollection<ParentOption> ParentOptions { get; } = [NoParent];

    public bool IsNew => _editing is null;

    public override string Title => IsNew ? "Nowy dział" : $"Dział – {_editing!.Department.Name}";

    /// <summary>The department after a successful save.</summary>
    public DepartmentResponse? Saved { get; private set; }

    [ObservableProperty]
    public partial string EditName { get; set; }

    [ObservableProperty]
    public partial string EditCode { get; set; }

    [ObservableProperty]
    public partial ParentOption EditParent { get; set; }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => !Validate() ? Task.CompletedTask : RunAsync(async () =>
    {
        Saved = _editing is { } current
            ? await _api.UpdateDepartmentAsync(current.Id, new UpdateDepartmentRequest(EditName, EditCode, EditParent.Id), cancellationToken)
            : await _api.CreateDepartmentAsync(new CreateDepartmentRequest(EditName, EditCode, EditParent.Id), cancellationToken);

        HasChanges = true;
        Close(true);
    });

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "department.code_taken" => nameof(EditCode),
            "department.cycle" or "department.parent_not_found" => nameof(EditParent),
            _ => null,
        };

        if (field is not null)
        {
            SetError(field, exception.Message);
        }

        return field is not null;
    }

    private bool Validate()
    {
        SetError(nameof(EditName), string.IsNullOrWhiteSpace(EditName) ? "Podaj nazwę działu." : null);
        SetError(nameof(EditCode), string.IsNullOrWhiteSpace(EditCode) ? "Podaj kod działu, np. IT albo KAD." : null);

        return !HasErrors;
    }
}
