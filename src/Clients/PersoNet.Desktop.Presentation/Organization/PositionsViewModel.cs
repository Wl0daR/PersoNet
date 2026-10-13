using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Organization;

/// <summary>
/// "Stanowiska" screen: the list of positions and the editor of the selected or new one.
/// </summary>
public sealed partial class PositionsViewModel(IOrganizationApi api) : EditorViewModel
{
    public const int NameMaxLength = 200;

    public ObservableCollection<PositionResponse> Positions { get; } = [];

    /// <summary>True when there is nothing to show in the list, so the view can explain what to do instead.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial bool ShowInactive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNew), nameof(EditorTitle), nameof(ToggleActiveText))]
    [NotifyCanExecuteChangedFor(nameof(ToggleActiveCommand))]
    public partial PositionResponse? Selected { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    public bool IsNew => Selected is null;

    public string EditorTitle => IsNew ? "Nowe stanowisko" : "Edycja stanowiska";

    public string ToggleActiveText => Selected?.IsActive == false ? "Aktywuj" : "Dezaktywuj";

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(() => ReloadSelectingAsync(Selected?.Id, cancellationToken));

    [RelayCommand]
    public void New()
    {
        Selected = null;
        EditName = string.Empty;
        ClearAllErrors();
    }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => !Validate() ? Task.CompletedTask : RunAsync(async () =>
    {
        var saved = Selected is { } current
            ? await api.UpdatePositionAsync(current.Id, new UpdatePositionRequest(EditName), cancellationToken)
            : await api.CreatePositionAsync(new CreatePositionRequest(EditName), cancellationToken);

        await ReloadSelectingAsync(saved.Id, cancellationToken);
    });

    [RelayCommand(CanExecute = nameof(CanToggleActive))]
    public Task ToggleActiveAsync(CancellationToken cancellationToken) => Selected is not { } current
        ? Task.CompletedTask
        : RunAsync(async () =>
        {
            await api.SetPositionActiveAsync(current.Id, !current.IsActive, cancellationToken);
            await ReloadSelectingAsync(current.Id, cancellationToken);
        });

    private bool CanToggleActive() => Selected is not null;

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

    private bool Validate()
    {
        SetError(nameof(EditName), string.IsNullOrWhiteSpace(EditName) ? "Podaj nazwę stanowiska." : null);

        return !HasErrors;
    }

    partial void OnSelectedChanged(PositionResponse? value)
    {
        ClearAllErrors();

        if (value is not null)
        {
            EditName = value.Name;
        }
    }

    partial void OnShowInactiveChanged(bool value) => _ = LoadAsync(CancellationToken.None);

    private async Task ReloadSelectingAsync(Guid? id, CancellationToken cancellationToken)
    {
        var positions = await api.GetPositionsAsync(ShowInactive, cancellationToken);

        Positions.Clear();
        foreach (var position in positions)
        {
            Positions.Add(position);
        }

        IsEmpty = Positions.Count == 0;

        var reselected = positions.FirstOrDefault(position => position.Id == id);
        if (reselected is null)
        {
            New();
        }
        else
        {
            Selected = reselected;
        }
    }
}
