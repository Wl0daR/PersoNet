using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Organization;

/// <summary>
/// "Stanowiska" screen: the list of positions across the window with their departments. A new or changed position is
/// filled in a dialog.
/// </summary>
public sealed partial class PositionsViewModel(IOrganizationApi api, IDialogService dialogs) : EditorViewModel
{
    private IReadOnlyList<DepartmentResponse> _departments = [];

    public ObservableCollection<PositionRow> Positions { get; } = [];

    /// <summary>True when there is nothing to show in the list, so the view can explain what to do instead.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial bool ShowInactive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleActiveText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand), nameof(EditCommand))]
    public partial PositionRow? Selected { get; set; }

    public string ToggleActiveText => Selected?.IsActive == false ? "Aktywuj" : "Dezaktywuj";

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(() => ReloadSelectingAsync(Selected?.Id, cancellationToken));

    [RelayCommand]
    public Task NewAsync(CancellationToken cancellationToken) => EditInDialogAsync(new PositionDialogViewModel(api, _departments, editing: null), cancellationToken);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    public Task EditAsync(PositionRow? position, CancellationToken cancellationToken) => (position ?? Selected) is not { } current
        ? Task.CompletedTask
        : EditInDialogAsync(new PositionDialogViewModel(api, _departments, current.Position), cancellationToken);

    [RelayCommand(CanExecute = nameof(CanToggleActive))]
    public Task ToggleActiveAsync(CancellationToken cancellationToken) => Selected is not { } current
        ? Task.CompletedTask
        : RunAsync(async () =>
        {
            await api.SetPositionActiveAsync(current.Id, !current.IsActive, cancellationToken);
            await ReloadSelectingAsync(current.Id, cancellationToken);
        });

    protected override bool TryShowOnField(ApiException exception) => false;

    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync(CancellationToken.None);

    private async Task EditInDialogAsync(PositionDialogViewModel dialog, CancellationToken cancellationToken)
    {
        if (await dialogs.ShowAsync(dialog))
        {
            await RunAsync(() => ReloadSelectingAsync(dialog.Saved?.Id, cancellationToken));
        }
    }

    private bool CanEdit(PositionRow? position) => (position ?? Selected) is not null;

    private bool CanToggleActive() => Selected is not null;

    private async Task ReloadSelectingAsync(Guid? id, CancellationToken cancellationToken)
    {
        var positions = await api.GetPositionsAsync(ShowInactive, cancellationToken);
        _departments = await api.GetDepartmentsAsync(includeInactive: true, cancellationToken);
        var names = _departments.ToDictionary(department => department.Id, department => department.Name);

        Positions.Clear();
        foreach (var position in positions)
        {
            var departments = position.DepartmentIds.Select(id => names.GetValueOrDefault(id)).OfType<string>().Order(StringComparer.CurrentCulture).ToList();
            Positions.Add(new PositionRow(position, departments.Count == 0 ? "każdy dział" : string.Join(", ", departments)));
        }

        IsEmpty = Positions.Count == 0;
        Selected = Positions.FirstOrDefault(position => position.Id == id);
    }
}

/// <summary>A position in the list, with the names of its departments ready to show.</summary>
public sealed record PositionRow(PositionResponse Position, string Departments)
{
    public Guid Id => Position.Id;

    public string Name => Position.Name;

    public bool IsActive => Position.IsActive;

    public bool HasDepartments => Position.DepartmentIds.Count > 0;
}
