using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Employees;
using PersoNet.Contracts.Audit;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Employees;

/// <summary>
/// "Historia" tab of an employee's card: who changed the card, contracts, leave and portal account, and when, newest first.
/// Loaded when the tab is opened, so it always includes what was just changed in the other tabs.
/// </summary>
public sealed partial class EmployeeHistoryViewModel(IEmployeesApi api) : EditorViewModel
{
    public const string AllSubjects = "Wszystkie zmiany";

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    private EmployeeResponse? _employee;
    private List<HistoryItem> _all = [];
    private int _loadVersion;

    public ObservableCollection<HistoryItem> Items { get; } = [];

    /// <summary>"Wszystkie zmiany" and the kinds of records present in the history, to narrow the list down.</summary>
    public ObservableCollection<string> Subjects { get; } = [AllSubjects];

    [ObservableProperty]
    public partial string Subject { get; set; } = AllSubjects;

    /// <summary>True while the tab is shown; opening it loads the history.</summary>
    [ObservableProperty]
    public partial bool IsActive { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEmpty), nameof(Summary))]
    public partial bool IsLoaded { get; set; }

    public bool IsEmpty => IsLoaded && Items.Count == 0;

    public string Summary => !IsLoaded ? string.Empty
        : Subject == AllSubjects ? Count(_all.Count)
        : $"{Count(Items.Count)} z {_all.Count}";

    public string EmptyText => _all.Count == 0
        ? "Nie ma jeszcze zapisanych zmian. Pojawią się tu, gdy ktoś zmieni dane, umowy, urlop lub konto pracownika."
        : "Brak zmian tego rodzaju.";

    /// <summary>Follows the card: the history of another employee is loaded when the tab is shown.</summary>
    public Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        ++_loadVersion;
        Clear();

        return IsActive ? LoadAsync(cancellationToken) : Task.CompletedTask;
    }

    [RelayCommand]
    public Task RefreshAsync(CancellationToken cancellationToken) => LoadAsync(cancellationToken);

    partial void OnIsActiveChanged(bool value)
    {
        if (value)
        {
            _ = LoadAsync(CancellationToken.None);
        }
    }

    partial void OnSubjectChanged(string value) => Filter();

    private Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee)
        {
            return Task.CompletedTask;
        }

        var version = ++_loadVersion;
        return RunAsync(async () =>
        {
            var history = await api.GetHistoryAsync(employee.Id, cancellationToken);
            if (version != _loadVersion)
            {
                return;
            }

            _all = history.Select(ToItem).ToList();

            var subject = Subject;
            Subjects.Clear();
            Subjects.Add(AllSubjects);
            foreach (var name in _all.Select(item => item.Subject).Distinct().Order(StringComparer.Create(Polish, ignoreCase: true)))
            {
                Subjects.Add(name);
            }

            Subject = Subjects.Contains(subject) ? subject : AllSubjects;
            Filter();
            IsLoaded = true;
        });
    }

    private void Clear()
    {
        _all = [];
        Items.Clear();
        IsLoaded = false;
        OnPropertyChanged(nameof(IsEmpty));
    }

    private void Filter()
    {
        Items.Clear();
        foreach (var item in _all.Where(item => Subject == AllSubjects || item.Subject == Subject))
        {
            Items.Add(item);
        }

        OnPropertyChanged(nameof(IsEmpty));
        OnPropertyChanged(nameof(Summary));
        OnPropertyChanged(nameof(EmptyText));
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    private static HistoryItem ToItem(HistoryEntryResponse entry) => new(
        entry.OccurredAt.ToLocalTime().ToString("d MMMM yyyy, HH:mm", Polish),
        entry.UserName,
        entry.Subject,
        entry.Description,
        entry.Action,
        entry.Changes.Select(change => new HistoryChange(change.Field, Text(change.OldValue), Text(change.NewValue), entry.Action)).ToList());

    private static string Text(string? value) => string.IsNullOrWhiteSpace(value) ? "(puste)" : value;

    private static string Count(int count)
    {
        var lastTwo = count % 100;
        var last = count % 10;
        var word = count == 1 ? "zmiana" : last is >= 2 and <= 4 && lastTwo is < 12 or > 14 ? "zmiany" : "zmian";
        return $"{count} {word}";
    }
}

/// <summary>One entry of the history list.</summary>
public sealed record HistoryItem(string When, string Who, string Subject, string Description, HistoryAction Action, IReadOnlyList<HistoryChange> Changes)
{
    public string WhenAndWho => $"{When} · {Who}";

    public bool HasChanges => Changes.Count > 0;
}

/// <summary>A field before and after; for a new record only the value matters, for a removed one only the old value.</summary>
public sealed record HistoryChange(string Field, string OldValue, string NewValue, HistoryAction Action)
{
    public string Text => Action switch
    {
        HistoryAction.Created => $"{Field}: {NewValue}",
        HistoryAction.Deleted => $"{Field}: {OldValue}",
        _ => $"{Field}: {OldValue} → {NewValue}",
    };
}
