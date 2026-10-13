using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Organization;

/// <summary>
/// "Działy" screen: the department tree across the window. A new or changed department is filled in a dialog.
/// </summary>
public sealed partial class DepartmentsViewModel(IOrganizationApi api, IDialogService dialogs) : EditorViewModel
{
    public ObservableCollection<DepartmentNode> Roots { get; } = [];

    /// <summary>True when there is nothing to show in the tree, so the view can explain what to do instead.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial bool ShowInactive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleActiveText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand), nameof(EditCommand))]
    public partial DepartmentNode? Selected { get; set; }

    public string ToggleActiveText => Selected?.Department.IsActive == false ? "Aktywuj" : "Dezaktywuj";

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) =>
        RunAsync(() => ReloadSelectingAsync(Selected?.Id, cancellationToken));

    /// <summary>Opens the form of a new department, placed under the selected one.</summary>
    [RelayCommand]
    public Task NewAsync(CancellationToken cancellationToken) =>
        EditInDialogAsync(new DepartmentDialogViewModel(api, Roots, editing: null, Selected?.Id), cancellationToken);

    /// <summary>Opens the selected department in the form.</summary>
    [RelayCommand(CanExecute = nameof(CanEdit))]
    public Task EditAsync(DepartmentNode? node, CancellationToken cancellationToken) => (node ?? Selected) is not { } department
        ? Task.CompletedTask
        : EditInDialogAsync(new DepartmentDialogViewModel(api, Roots, department, parentId: null), cancellationToken);

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

    protected override bool TryShowOnField(ApiException exception) => false;

    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync(CancellationToken.None);

    private async Task EditInDialogAsync(DepartmentDialogViewModel dialog, CancellationToken cancellationToken)
    {
        if (await dialogs.ShowAsync(dialog))
        {
            await RunAsync(() => ReloadSelectingAsync(dialog.Saved?.Id, cancellationToken));
        }
    }

    private bool CanEdit(DepartmentNode? node) => (node ?? Selected) is not null;

    private bool CanToggleActive() => Selected is not null;

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
    }
}
