using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Organization;

/// <summary>
/// "Stanowiska" screen: the list of positions across the window. A new or renamed position is filled in a dialog.
/// </summary>
public sealed partial class PositionsViewModel(IOrganizationApi api, IDialogService dialogs) : EditorViewModel
{
    public ObservableCollection<PositionResponse> Positions { get; } = [];

    /// <summary>True when there is nothing to show in the list, so the view can explain what to do instead.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial bool ShowInactive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ToggleActiveText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand), nameof(EditCommand))]
    public partial PositionResponse? Selected { get; set; }

    public string ToggleActiveText => Selected?.IsActive == false ? "Aktywuj" : "Dezaktywuj";

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(() => ReloadSelectingAsync(Selected?.Id, cancellationToken));

    [RelayCommand]
    public Task NewAsync(CancellationToken cancellationToken) => EditInDialogAsync(new PositionDialogViewModel(api, editing: null), cancellationToken);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    public Task EditAsync(PositionResponse? position, CancellationToken cancellationToken) => (position ?? Selected) is not { } current
        ? Task.CompletedTask
        : EditInDialogAsync(new PositionDialogViewModel(api, current), cancellationToken);

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

    private bool CanEdit(PositionResponse? position) => (position ?? Selected) is not null;

    private bool CanToggleActive() => Selected is not null;

    private async Task ReloadSelectingAsync(Guid? id, CancellationToken cancellationToken)
    {
        var positions = await api.GetPositionsAsync(ShowInactive, cancellationToken);

        Positions.Clear();
        foreach (var position in positions)
        {
            Positions.Add(position);
        }

        IsEmpty = Positions.Count == 0;
        Selected = Positions.FirstOrDefault(position => position.Id == id);
    }
}
