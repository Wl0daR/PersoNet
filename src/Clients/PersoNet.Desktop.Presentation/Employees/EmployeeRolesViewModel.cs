using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Access;
using PersoNet.Contracts.Access;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Employees;

/// <summary>
/// Roles of the employee, in the "Konto" tab of the card: a check box per company role. The employee has the permissions of
/// the checked roles when they sign in with their work e-mail.
/// </summary>
public sealed partial class EmployeeRolesViewModel(IAccessApi api) : EditorViewModel
{
    private EmployeeResponse? _employee;
    private HashSet<Guid> _saved = [];
    private int _loadVersion;

    public ObservableCollection<RoleOption> Options { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool HasChanges { get; set; }

    [ObservableProperty]
    public partial string? SavedMessage { get; set; }

    [ObservableProperty]
    public partial bool IsLoaded { get; set; }

    public bool HasNoRoles => IsLoaded && Options.Count == 0;

    public async Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        var version = ++_loadVersion;
        Reset([], [], loaded: false);

        if (employee is null)
        {
            return;
        }

        await RunAsync(async () =>
        {
            var roles = await api.ListRolesAsync(cancellationToken);
            var granted = await api.GetEmployeeRolesAsync(employee.Id, cancellationToken);
            if (version == _loadVersion)
            {
                Reset(roles, granted, loaded: true);
            }
        });
    }

    [RelayCommand(CanExecute = nameof(HasChanges))]
    public Task SaveAsync(CancellationToken cancellationToken) => _employee is not { } employee
        ? Task.CompletedTask
        : RunAsync(async () =>
        {
            var granted = await api.SetEmployeeRolesAsync(employee.Id, Options.Where(option => option.IsChecked).Select(option => option.Id).ToList(), cancellationToken);
            _saved = granted.Select(role => role.Id).ToHashSet();
            HasChanges = false;
            SavedMessage = granted.Count == 0 ? "Zapisano. Pracownik nie ma żadnej roli." : "Zapisano. Pracownik ma teraz uprawnienia zaznaczonych ról.";
        });

    protected override bool TryShowOnField(ApiException exception) => false;

    private void Reset(IReadOnlyList<RoleResponse> roles, IReadOnlyList<RoleResponse> granted, bool loaded)
    {
        foreach (var option in Options)
        {
            option.PropertyChanged -= OnOptionChanged;
        }

        _saved = granted.Select(role => role.Id).ToHashSet();
        Options.Clear();
        foreach (var role in roles)
        {
            var option = new RoleOption(role, _saved.Contains(role.Id));
            option.PropertyChanged += OnOptionChanged;
            Options.Add(option);
        }

        HasChanges = false;
        SavedMessage = null;
        IsLoaded = loaded;
        OnPropertyChanged(nameof(HasNoRoles));
    }

    private void OnOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        HasChanges = !Options.Where(option => option.IsChecked).Select(option => option.Id).ToHashSet().SetEquals(_saved);
        SavedMessage = null;
    }
}

/// <summary>A company role with a check box.</summary>
public sealed partial class RoleOption(RoleResponse role, bool isChecked) : ObservableObject
{
    public Guid Id => role.Id;

    public string Name => role.Name;

    public string Description => role.Description ?? string.Empty;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;
}
