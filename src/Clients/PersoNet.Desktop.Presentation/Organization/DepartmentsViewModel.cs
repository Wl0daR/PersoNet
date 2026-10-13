using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Organization;

/// <summary>
/// "Działy" screen: the department tree on the left, the editor of the selected or new department on the right.
/// </summary>
public sealed partial class DepartmentsViewModel(IOrganizationApi api) : EditorViewModel
{
    public const int NameMaxLength = 200;

    public const int CodeMaxLength = 20;

    public static readonly ParentOption NoParent = new(null, "(brak – dział główny)");

    public ObservableCollection<DepartmentNode> Roots { get; } = [];

    /// <summary>True when there is nothing to show in the tree, so the view can explain what to do instead.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public ObservableCollection<ParentOption> ParentOptions { get; } = [NoParent];

    [ObservableProperty]
    public partial bool ShowInactive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew), nameof(EditorTitle), nameof(ToggleActiveText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    public partial DepartmentNode? Selected { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditCode { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ParentOption EditParent { get; set; } = NoParent;

    public bool IsNew => Selected is null;

    public string EditorTitle => IsNew ? "Nowy dział" : "Edycja działu";

    public string ToggleActiveText => Selected?.Department.IsActive == false ? "Aktywuj" : "Dezaktywuj";

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) =>
        RunAsync(() => ReloadSelectingAsync(Selected?.Id, cancellationToken));

    /// <summary>Clears the editor for a new department, placed under the currently selected one.</summary>
    [RelayCommand]
    public void New()
    {
        var parent = Selected;
        StartNew();

        if (parent is not null)
        {
            EditParent = ParentOptions.FirstOrDefault(option => option.Id == parent.Id) ?? NoParent;
        }
    }

    [RelayCommand]
    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        if (!Validate())
        {
            return;
        }

        await RunAsync(async () =>
        {
            DepartmentResponse saved = Selected is { } current
                ? await api.UpdateDepartmentAsync(current.Id, new UpdateDepartmentRequest(EditName, EditCode, EditParent.Id), cancellationToken)
                : await api.CreateDepartmentAsync(new CreateDepartmentRequest(EditName, EditCode, EditParent.Id), cancellationToken);

            await ReloadSelectingAsync(saved.Id, cancellationToken);
        });
    }

    [RelayCommand(CanExecute = nameof(CanToggleActive))]
    public async Task ToggleActiveAsync(CancellationToken cancellationToken)
    {
        if (Selected is not { } current)
        {
            return;
        }

        await RunAsync(async () =>
        {
            await api.SetDepartmentActiveAsync(current.Id, !current.Department.IsActive, cancellationToken);
            await ReloadSelectingAsync(current.Id, cancellationToken);
        });
    }

    private bool CanToggleActive() => Selected is not null;

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

    partial void OnSelectedChanged(DepartmentNode? value)
    {
        ClearAllErrors();
        RefreshParentOptions(value);

        if (value is null)
        {
            return;
        }

        EditName = value.Department.Name;
        EditCode = value.Department.Code;
        EditParent = ParentOptions.FirstOrDefault(option => option.Id == value.Department.ParentId) ?? NoParent;
    }

    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync(CancellationToken.None);

    private void StartNew()
    {
        Selected = null;
        ClearAllErrors();
        RefreshParentOptions(editing: null);
        EditName = string.Empty;
        EditCode = string.Empty;
        EditParent = NoParent;
    }

    // A department cannot be moved under itself or its own sub-departments, so those are not offered.
    private void RefreshParentOptions(DepartmentNode? editing)
    {
        var excluded = editing?.SelfAndDescendants().Select(node => node.Id).ToHashSet() ?? [];

        ParentOptions.Clear();
        ParentOptions.Add(NoParent);

        foreach (var node in Roots.SelectMany(root => root.SelfAndDescendants()).Where(node => !excluded.Contains(node.Id)))
        {
            ParentOptions.Add(new ParentOption(node.Id, node.DisplayName));
        }
    }

    private async Task ReloadSelectingAsync(Guid? id, CancellationToken cancellationToken)
    {
        var departments = await api.GetDepartmentsAsync(ShowInactive, cancellationToken);

        Roots.Clear();
        foreach (var root in DepartmentNode.BuildTree(departments))
        {
            Roots.Add(root);
        }

        IsEmpty = Roots.Count == 0;

        Selected = Roots.SelectMany(root => root.SelfAndDescendants()).FirstOrDefault(node => node.Id == id);
        if (Selected is null)
        {
            StartNew();
        }
    }
}
