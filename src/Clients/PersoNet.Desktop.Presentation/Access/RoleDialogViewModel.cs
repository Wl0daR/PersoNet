using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Access;
using PersoNet.Contracts.Access;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Access;

/// <summary>Dialog of a new role or of the one being changed: its name, a description and the permissions it grants.</summary>
public sealed partial class RoleDialogViewModel : DialogViewModel
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    private readonly IAccessApi _api;
    private readonly RoleResponse? _editing;

    public RoleDialogViewModel(IAccessApi api, IReadOnlyList<PermissionResponse> catalog, RoleResponse? editing)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        _api = api;
        _editing = editing;
        EditName = editing?.Name ?? string.Empty;
        EditDescription = editing?.Description ?? string.Empty;

        var granted = editing?.Permissions.ToHashSet(StringComparer.Ordinal) ?? [];
        Groups = [.. catalog
            .GroupBy(permission => permission.Group)
            .Select(group => new PermissionGroup(group.Key, [.. group.Select(permission => new PermissionOption(permission, granted.Contains(permission.Code)))]))];
        ClearAllErrors();
    }

    public bool IsNew => _editing is null;

    public override string Title => IsNew ? "Nowa rola" : $"Rola – {_editing!.Name}";

    /// <summary>Permissions by area ("Pracownicy", "Urlopy", "Firma").</summary>
    public ObservableCollection<PermissionGroup> Groups { get; }

    /// <summary>The role after a successful save.</summary>
    public RoleResponse? Saved { get; private set; }

    [ObservableProperty]
    public partial string EditName { get; set; }

    [ObservableProperty]
    public partial string EditDescription { get; set; }

    public IReadOnlyList<string> CheckedPermissions =>
        Groups.SelectMany(group => group.Items).Where(option => option.IsChecked).Select(option => option.Code).ToList();

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => !Validate() ? Task.CompletedTask : RunAsync(async () =>
    {
        var request = new SaveRoleRequest(EditName, EditDescription, CheckedPermissions);
        Saved = _editing is { } current
            ? await _api.UpdateRoleAsync(current.Id, request, cancellationToken)
            : await _api.CreateRoleAsync(request, cancellationToken);

        HasChanges = true;
        Close(true);
    });

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Code is not ("role.name_taken" or "role.name_required" or "role.name_too_long"))
        {
            return false;
        }

        SetError(nameof(EditName), exception.Message);
        return true;
    }

    private bool Validate()
    {
        SetError(nameof(EditName), string.IsNullOrWhiteSpace(EditName) ? "Podaj nazwę roli."
            : EditName.Trim().Length > NameMaxLength ? $"Nazwa może mieć najwyżej {NameMaxLength} znaków."
            : null);
        SetError(nameof(EditDescription), EditDescription.Trim().Length > DescriptionMaxLength ? $"Opis może mieć najwyżej {DescriptionMaxLength} znaków." : null);

        return !HasErrors;
    }
}

/// <summary>Permissions of one area, shown under its heading.</summary>
public sealed record PermissionGroup(string Name, IReadOnlyList<PermissionOption> Items);

/// <summary>A permission with a check box; ones only the HR administrator has are shown but cannot be checked.</summary>
public sealed partial class PermissionOption(PermissionResponse permission, bool isChecked) : ObservableObject
{
    public string Code => permission.Code;

    public string Name => permission.Name;

    public string Description => permission.Description;

    public bool IsEnabled => permission.Assignable;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked && permission.Assignable;
}
