using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Organization;
using PersoNet.Contracts.Organization;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Organization;

/// <summary>Dialog with the name of a new position or of the one being changed.</summary>
public sealed partial class PositionDialogViewModel : DialogViewModel
{
    public const int NameMaxLength = 200;

    private readonly IOrganizationApi _api;
    private readonly PositionResponse? _editing;

    public PositionDialogViewModel(IOrganizationApi api, PositionResponse? editing)
    {
        _api = api;
        _editing = editing;
        EditName = editing?.Name ?? string.Empty;
        ClearAllErrors();
    }

    public bool IsNew => _editing is null;

    public override string Title => IsNew ? "Nowe stanowisko" : $"Stanowisko – {_editing!.Name}";

    /// <summary>The position after a successful save.</summary>
    public PositionResponse? Saved { get; private set; }

    [ObservableProperty]
    public partial string EditName { get; set; }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => !Validate() ? Task.CompletedTask : RunAsync(async () =>
    {
        Saved = _editing is { } current
            ? await _api.UpdatePositionAsync(current.Id, new UpdatePositionRequest(EditName), cancellationToken)
            : await _api.CreatePositionAsync(new CreatePositionRequest(EditName), cancellationToken);

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

    private bool Validate()
    {
        SetError(nameof(EditName), string.IsNullOrWhiteSpace(EditName) ? "Podaj nazwę stanowiska." : null);

        return !HasErrors;
    }
}
