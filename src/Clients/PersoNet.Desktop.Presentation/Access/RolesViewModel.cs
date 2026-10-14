using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Access;
using PersoNet.Contracts.Access;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Access;

/// <summary>
/// "Role" screen: the roles the company defines and what each permits. A role is set up in a dialog and given to people in
/// their card (tab Konto). The HR administrator always has every permission, so nobody can lock HR out.
/// </summary>
public sealed partial class RolesViewModel(IAccessApi api, IDialogService dialogs) : EditorViewModel
{
    private IReadOnlyList<PermissionResponse> _catalog = [];

    public ObservableCollection<RoleRow> Items { get; } = [];

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteText))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(DeleteCommand))]
    public partial RoleRow? Selected { get; set; }

    /// <summary>First click on "Usuń" asks for confirmation; the second one removes the role.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DeleteText))]
    public partial bool IsConfirmingDelete { get; set; }

    public string DeleteText => IsConfirmingDelete ? "Na pewno usunąć?" : "Usuń";

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(() => ReloadSelectingAsync(Selected?.Id, cancellationToken));

    [RelayCommand]
    public Task NewAsync(CancellationToken cancellationToken) => EditInDialogAsync(editing: null, cancellationToken);

    [RelayCommand(CanExecute = nameof(CanEdit))]
    public Task EditAsync(RoleRow? row, CancellationToken cancellationToken) => (row ?? Selected) is not { } current
        ? Task.CompletedTask
        : EditInDialogAsync(current.Role, cancellationToken);

    [RelayCommand(CanExecute = nameof(CanDelete))]
    public Task DeleteAsync(CancellationToken cancellationToken)
    {
        if (Selected is not { } current)
        {
            return Task.CompletedTask;
        }

        if (!IsConfirmingDelete)
        {
            IsConfirmingDelete = true;
            return Task.CompletedTask;
        }

        IsConfirmingDelete = false;
        return RunAsync(async () =>
        {
            await api.DeleteRoleAsync(current.Id, cancellationToken);
            await ReloadSelectingAsync(null, cancellationToken);
        });
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    partial void OnSelectedChanged(RoleRow? value) => IsConfirmingDelete = false;

    private async Task EditInDialogAsync(RoleResponse? editing, CancellationToken cancellationToken)
    {
        IsConfirmingDelete = false;

        if (_catalog.Count == 0)
        {
            await RunAsync(async () => _catalog = await api.ListPermissionsAsync(cancellationToken));
            if (_catalog.Count == 0)
            {
                return;
            }
        }

        var dialog = new RoleDialogViewModel(api, _catalog, editing);
        if (await dialogs.ShowAsync(dialog))
        {
            await RunAsync(() => ReloadSelectingAsync(dialog.Saved?.Id, cancellationToken));
        }
    }

    private bool CanEdit(RoleRow? row) => (row ?? Selected) is not null;

    private bool CanDelete() => Selected is not null;

    private async Task ReloadSelectingAsync(Guid? id, CancellationToken cancellationToken)
    {
        if (_catalog.Count == 0)
        {
            _catalog = await api.ListPermissionsAsync(cancellationToken);
        }

        var roles = await api.ListRolesAsync(cancellationToken);
        var names = _catalog.ToDictionary(permission => permission.Code, permission => permission.Name, StringComparer.Ordinal);

        Items.Clear();
        foreach (var role in roles)
        {
            Items.Add(new RoleRow(role, names));
        }

        IsEmpty = Items.Count == 0;
        Selected = Items.FirstOrDefault(row => row.Id == id);
    }
}

/// <summary>A role in the list, with its permissions named the way HR reads them.</summary>
public sealed class RoleRow(RoleResponse role, IReadOnlyDictionary<string, string> permissionNames)
{
    public RoleResponse Role { get; } = role;

    public Guid Id => Role.Id;

    public string Name => Role.Name;

    public string Description => Role.Description ?? string.Empty;

    public string PermissionsText => Role.Permissions.Count == 0
        ? "Brak uprawnień"
        : string.Join(", ", Role.Permissions.Select(code => permissionNames.GetValueOrDefault(code, code)));

    public string MembersText => Role.MemberCount switch
    {
        0 => "nikt",
        1 => "1 osoba",
        var count when count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14 => $"{count} osoby",
        var count => $"{count} osób",
    };
}
